using System.Globalization;
using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class AttendanceDisputeService : IAttendanceDisputeService
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeRolloverService _codeRolloverService;
        private readonly ICacheService _cache;
        private readonly ILogger<AttendanceDisputeService> _logger;

        public AttendanceDisputeService(
            ParishDbContext context,
            IStudentCodeRolloverService codeRolloverService,
            ICacheService cache,
            ILogger<AttendanceDisputeService> logger)
        {
            _context = context;
            _codeRolloverService = codeRolloverService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(bool Success, string Message)> SubmitDisputeFromParentAsync(
            SubmitAttendanceDisputeRequestDto dto,
            string ipAddress)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return (false, "Vui lòng nhập đầy đủ nội dung báo sai sót điểm danh.");
            }

            if (!DateTime.TryParseExact(
                    dto.AttendanceDateIso?.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
            {
                return (false, "Định dạng ngày điểm danh không hợp lệ.");
            }

            var targetDateUtc = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            var todayVn = DateTime.UtcNow.AddHours(7).Date;
            if (targetDateUtc.Date > todayVn)
            {
                return (false, "Không thể gửi yêu cầu báo sai sót cho ngày chưa diễn ra.");
            }

            // 1. Xác thực 3 yếu tố: Mã thiếu nhi (kể cả mã lịch sử) + Ngày sinh + 4 số cuối SĐT Phụ huynh
            var (resolvedStudent, _, _) = await _codeRolloverService.ResolveStudentByAnyCodeAsync(dto.StudentCode);
            var startDob = DateTime.SpecifyKind(dto.DateOfBirth.Date, DateTimeKind.Utc);
            var endDob = startDob.AddDays(1);

            if (resolvedStudent == null ||
                !resolvedStudent.IsActive ||
                resolvedStudent.DateOfBirth < startDob ||
                resolvedStudent.DateOfBirth >= endDob)
            {
                return (false, "Thông tin Mã thiếu nhi hoặc Ngày sinh không khớp với hồ sơ.");
            }

            var phoneDigits = new string((resolvedStudent.ParentPhone ?? string.Empty).Where(char.IsDigit).ToArray());
            if (phoneDigits.Length < 4 || !phoneDigits.EndsWith(dto.PhoneLast4Digits.Trim()))
            {
                return (false, "4 chữ số cuối Số điện thoại Phụ huynh không chính xác.");
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
                              ?? await _context.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

            if (currentYear == null)
            {
                return (false, "Chưa có niên khóa hoạt động.");
            }

            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.ClassRoom)
                .FirstOrDefaultAsync(e => e.StudentId == resolvedStudent.Id &&
                                          (e.AcademicYearId == currentYear.Id || e.ClassRoom.AcademicYearId == currentYear.Id));

            if (enrollment == null || enrollment.ClassRoom == null)
            {
                return (false, "Thiếu nhi hiện chưa được xếp lớp trong niên khóa này.");
            }

            // 2. Chống Spam: Giới hạn tối đa 5 yêu cầu PENDING trên mỗi học sinh
            int pendingCount = await _context.AttendanceDisputeRequests
                .CountAsync(d => d.EnrollmentId == enrollment.Id && d.Status == "PENDING");

            if (pendingCount >= 5)
            {
                return (false, "Hồ sơ đang có 5 yêu cầu báo sai sót chờ Giáo lý viên duyệt. Vui lòng chờ Giáo lý viên xử lý trước khi gửi thêm.");
            }

            var normalizedScope = (dto.SessionScope ?? "REGULAR_MASS_SUN").Trim().ToUpperInvariant();
            var allowedScopes = new[] { "REGULAR_MASS_SUN", "REGULAR_MASS_THU", "REGULAR_CLASS", "CAMPAIGN_MASS" };
            if (!allowedScopes.Contains(normalizedScope))
            {
                return (false, "Loại buổi sinh hoạt cần báo sai sót không hợp lệ.");
            }

            bool duplicatePending = await _context.AttendanceDisputeRequests
                .AnyAsync(d => d.EnrollmentId == enrollment.Id &&
                               d.AttendanceDate == targetDateUtc &&
                               d.SessionScope == normalizedScope &&
                               d.Status == "PENDING");

            if (duplicatePending)
            {
                return (false, $"Gia đình đã gửi yêu cầu kiểm tra lại cho mục '{GetScopeDisplayLabel(normalizedScope)}' ngày {targetDateUtc:dd/MM/yyyy} và đang chờ Giáo lý viên phản hồi.");
            }

            // 3. Xác định trạng thái đang ghi nhận hiện tại trên hệ thống
            string currentRecordedStatus = AttendanceStatus.AbsentUnpermitted;
            if (normalizedScope == "CAMPAIGN_MASS" && dto.CampaignId.HasValue)
            {
                var campRec = await _context.MassCampaignRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CampaignId == dto.CampaignId.Value &&
                                              r.EnrollmentId == enrollment.Id &&
                                              r.AttendanceDate == targetDateUtc);
                currentRecordedStatus = campRec?.Status ?? "ABSENT";
            }
            else
            {
                var attRec = await _context.Attendances
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.EnrollmentId == enrollment.Id && a.AttendanceDate == targetDateUtc);

                if (attRec != null)
                {
                    currentRecordedStatus = normalizedScope == "REGULAR_CLASS"
                        ? attRec.ClassStatus
                        : attRec.MassStatus;
                }
            }

            var normalizedRequested = (dto.RequestedStatus ?? AttendanceStatus.Present).Trim().ToUpperInvariant();
            var allowedRequested = new[] { "PRESENT", "LINED_UP", "LATE", "ABSENT_PERMITTED", "EXCUSED" };
            if (!allowedRequested.Contains(normalizedRequested))
            {
                normalizedRequested = AttendanceStatus.Present;
            }

            var dispute = new AttendanceDisputeRequest
            {
                EnrollmentId = enrollment.Id,
                StudentId = resolvedStudent.Id,
                ClassRoomId = enrollment.ClassRoomId,
                CampaignId = dto.CampaignId,
                AttendanceDate = targetDateUtc,
                SessionScope = normalizedScope,
                CurrentRecordedStatus = currentRecordedStatus,
                RequestedStatus = normalizedRequested,
                Reason = dto.Reason.Trim(),
                ParentPhoneLast4 = dto.PhoneLast4Digits.Trim(),
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.AttendanceDisputeRequests.Add(dispute);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT SUBMIT_DISPUTE: Student={Code}, Class={ClassName}, Date={Date:yyyy-MM-dd}, Scope={Scope}, Old={Old} -> Req={Req}, IP={IP}",
                resolvedStudent.StudentCode, enrollment.ClassRoom.Name, targetDateUtc, normalizedScope, currentRecordedStatus, normalizedRequested, ipAddress);

            return (true, $"Đã gửi yêu cầu đính chính điểm danh ngày {targetDateUtc:dd/MM/yyyy} tới Giáo lý viên chủ nhiệm lớp {enrollment.ClassRoom.Name}!");
        }

        public async Task<int> GetPendingDisputeCountForClassAsync(int classId)
        {
            if (classId <= 0) return 0;
            return await _context.AttendanceDisputeRequests
                .AsNoTracking()
                .CountAsync(d => d.ClassRoomId == classId && d.Status == "PENDING");
        }

        public async Task<List<object>> GetDisputesForClassAsync(
            int classId,
            string statusFilter,
            ClaimsPrincipal user)
        {
            if (!await CanUserManageClassDisputesAsync(user, classId))
            {
                return new List<object>();
            }

            var query = _context.AttendanceDisputeRequests
                .AsNoTracking()
                .Include(d => d.Student)
                .Where(d => d.ClassRoomId == classId);

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "ALL")
            {
                var norm = statusFilter.Trim().ToUpperInvariant();
                query = query.Where(d => d.Status == norm);
            }

            var items = await query
                .OrderByDescending(d => d.Status == "PENDING")
                .ThenByDescending(d => d.CreatedAt)
                .Take(100)
                .ToListAsync();

            return items.Select(d => (object)new
            {
                id = d.Id,
                studentCode = d.Student?.StudentCode ?? "",
                studentName = d.Student != null
                    ? $"{d.Student.ChristianName} {d.Student.FirstName} {d.Student.LastName}".Trim()
                    : "",
                attendanceDate = d.AttendanceDate.ToString("dd/MM/yyyy"),
                attendanceDateIso = d.AttendanceDate.ToString("yyyy-MM-dd"),
                sessionScope = d.SessionScope,
                sessionScopeLabel = GetScopeDisplayLabel(d.SessionScope),
                currentRecordedStatus = d.CurrentRecordedStatus,
                currentStatusLabel = GetStatusDisplayLabel(d.CurrentRecordedStatus),
                requestedStatus = d.RequestedStatus,
                requestedStatusLabel = GetStatusDisplayLabel(d.RequestedStatus),
                reason = d.Reason,
                status = d.Status,
                teacherResponseNote = d.TeacherResponseNote ?? "",
                reviewedBy = d.ReviewedBy ?? "",
                createdAt = d.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm")
            }).ToList();
        }

        public async Task<(bool Success, string Message)> ReviewDisputeByTeacherAsync(
            ReviewAttendanceDisputeRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            var dispute = await _context.AttendanceDisputeRequests
                .Include(d => d.Student)
                .Include(d => d.ClassRoom)
                .FirstOrDefaultAsync(d => d.Id == dto.DisputeId);

            if (dispute == null)
            {
                return (false, "Không tìm thấy yêu cầu báo sai sót điểm danh.");
            }

            if (!await CanUserManageClassDisputesAsync(user, dispute.ClassRoomId))
            {
                return (false, "Bạn không có thẩm quyền xét duyệt yêu cầu của lớp này.");
            }

            if (dispute.Status != "PENDING")
            {
                return (false, "Yêu cầu này đã được xử lý trước đó.");
            }

            var decision = (dto.Decision ?? "").Trim().ToUpperInvariant();
            if (decision != "APPROVED" && decision != "REJECTED")
            {
                return (false, "Quyết định phê duyệt không hợp lệ.");
            }

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";
            var studentFullName = dispute.Student != null
                ? $"{dispute.Student.ChristianName} {dispute.Student.FirstName} {dispute.Student.LastName}".Trim()
                : $"#{dispute.StudentId}";

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    dispute.Status = decision;
                    dispute.TeacherResponseNote = string.IsNullOrWhiteSpace(dto.TeacherResponseNote)
                        ? (decision == "APPROVED" ? "Đã kiểm tra và đính chính trạng thái điểm danh." : "Không đủ cơ sở xác nhận.")
                        : dto.TeacherResponseNote.Trim();
                    dispute.ReviewedBy = operatorName;
                    dispute.ReviewedAt = DateTime.UtcNow;

                    if (decision == "APPROVED")
                    {
                        if (dispute.SessionScope == "CAMPAIGN_MASS" && dispute.CampaignId.HasValue)
                        {
                            var campRec = await _context.MassCampaignRecords
                                .FirstOrDefaultAsync(r => r.CampaignId == dispute.CampaignId.Value &&
                                                          r.EnrollmentId == dispute.EnrollmentId &&
                                                          r.AttendanceDate == dispute.AttendanceDate);

                            var mappedStatus = dispute.RequestedStatus == "ABSENT_PERMITTED" ? "EXCUSED"
                                             : (dispute.RequestedStatus == "LINED_UP" ? "PRESENT" : dispute.RequestedStatus);

                            if (campRec == null)
                            {
                                _context.MassCampaignRecords.Add(new MassCampaignRecord
                                {
                                    CampaignId = dispute.CampaignId.Value,
                                    StudentId = dispute.StudentId,
                                    EnrollmentId = dispute.EnrollmentId,
                                    AttendanceDate = dispute.AttendanceDate,
                                    Status = mappedStatus,
                                    CheckInMethod = "DISPUTE_FIX",
                                    CheckInTime = DateTime.UtcNow,
                                    Note = $"[Đính chính sai sót PH]: {dispute.Reason}",
                                    LastModifiedBy = operatorName,
                                    LastModifiedAt = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                campRec.Status = mappedStatus;
                                campRec.CheckInMethod = "DISPUTE_FIX";
                                campRec.Note = $"[Đính chính sai sót PH]: {dispute.Reason}";
                                campRec.LastModifiedBy = operatorName;
                                campRec.LastModifiedAt = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            var att = await _context.Attendances
                                .FirstOrDefaultAsync(a => a.EnrollmentId == dispute.EnrollmentId &&
                                                          a.AttendanceDate == dispute.AttendanceDate);

                            var oldValuesText = att != null
                                ? $"MassStatus: {att.MassStatus}, ClassStatus: {att.ClassStatus}"
                                : "NONE";

                            if (att == null)
                            {
                                att = new Attendance
                                {
                                    EnrollmentId = dispute.EnrollmentId,
                                    AttendanceDate = dispute.AttendanceDate,
                                    DayOfWeek = dispute.AttendanceDate.DayOfWeek,
                                    MassStatus = AttendanceStatus.AbsentUnpermitted,
                                    ClassStatus = AttendanceStatus.AbsentUnpermitted,
                                    CreatedAt = DateTime.UtcNow
                                };
                                _context.Attendances.Add(att);
                            }

                            var targetStatus = dispute.RequestedStatus == "EXCUSED"
                                ? AttendanceStatus.AbsentPermitted
                                : dispute.RequestedStatus;

                            bool isAttended = targetStatus == AttendanceStatus.Present ||
                                              targetStatus == AttendanceStatus.LinedUp ||
                                              targetStatus == AttendanceStatus.Late;

                            if (dispute.SessionScope == "REGULAR_CLASS")
                            {
                                att.ClassAttended = isAttended;
                                att.ClassStatus = targetStatus;
                            }
                            else
                            {
                                att.AttendedMass = isAttended;
                                att.MassStatus = targetStatus;
                            }

                            att.Note = $"[Đính chính theo báo sai sót PH]: {dispute.Reason}";
                            await _context.SaveChangesAsync();

                            _context.AttendanceAuditLogs.Add(new AttendanceAuditLog
                            {
                                AttendanceId = att.Id,
                                ActionType = "DISPUTE_APPROVED",
                                OldValues = oldValuesText,
                                NewValues = $"Scope: {dispute.SessionScope}, Status: {targetStatus}",
                                ModifiedBy = operatorName,
                                Reason = $"Duyệt báo sai sót PH ({GetScopeDisplayLabel(dispute.SessionScope)}): {dispute.Reason}",
                                IpAddress = ipAddress,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                // Xóa cache Sổ điểm danh Redis của lớp trong ngày đó để giao diện nạp lại dữ liệu mới nhất từ DB
                if (decision == "APPROVED")
                {
                    await _cache.RemoveAsync(RedisAttendanceKeys.ClassSheet(dispute.ClassRoomId, dispute.AttendanceDate));
                }

                _logger.LogInformation(
                    "AUDIT REVIEW_DISPUTE: DisputeId={Id}, Decision={Decision}, Student={StudentCode}, Operator={Operator}, IP={IP}",
                    dispute.Id, decision, dispute.Student?.StudentCode, operatorName, ipAddress);

                return (true, decision == "APPROVED"
                    ? $"Đã duyệt và tự động cập nhật điểm danh ngày {dispute.AttendanceDate:dd/MM/yyyy} cho em {studentFullName}!"
                    : $"Đã từ chối yêu cầu báo sai sót ngày {dispute.AttendanceDate:dd/MM/yyyy} của em {studentFullName}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi duyệt báo sai sót điểm danh DisputeId={Id}", dto.DisputeId);
                return (false, "Đã xảy ra lỗi khi lưu kết quả xét duyệt. Vui lòng thử lại.");
            }
        }

        private async Task<bool> CanUserManageClassDisputesAsync(ClaimsPrincipal user, int classId)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return false;

            if (user.IsInRole(UserRole.Admin) ||
                user.IsInRole(UserRole.SpiritualDirector) ||
                user.IsInRole(UserRole.ExecutiveBoard))
            {
                return true;
            }

            var classRoom = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
            if (classRoom == null) return false;

            if (user.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = user.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(managedGrade.Trim(), classRoom.GradeLevel.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdStr, out long userId))
            {
                return await _context.ClassTeachers
                    .AsNoTracking()
                    .AnyAsync(ct => ct.ClassRoomId == classId && ct.UserId == userId);
            }

            return false;
        }

        public static string GetScopeDisplayLabel(string scope) => scope switch
        {
            "REGULAR_MASS_SUN" => "Thánh Lễ Chúa Nhật",
            "REGULAR_MASS_THU" => "Thánh Lễ Thứ Năm",
            "REGULAR_CLASS" => "Giờ Học Giáo Lý CN",
            "CAMPAIGN_MASS" => "Lễ Chiến Dịch 1 Tháng",
            _ => scope
        };

        public static string GetStatusDisplayLabel(string status) => status switch
        {
            "LINED_UP" => "Xếp hàng đúng giờ",
            "PRESENT" => "Có mặt",
            "LATE" => "Đi trễ",
            "ABSENT_PERMITTED" or "EXCUSED" => "Vắng có phép",
            "ABSENT_UNPERMITTED" or "ABSENT" or "NONE" => "Vắng không phép",
            _ => status
        };
    }
}