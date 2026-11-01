using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class MassCampaignService : IMassCampaignService
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<MassCampaignService> _logger;

        public MassCampaignService(
            ParishDbContext context,
            ILogger<MassCampaignService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Chỉ kích hoạt chiến dịch đi lễ 1 tháng cho các lớp chuẩn bị lãnh nhận Bí tích hoặc Tuyên hứa:
        /// Rước Lễ 2 (RL2), Thêm Sức 3 (TS3), Bao Đồng 3 (BD3), Dự Bị 4 (DB4)
        /// </summary>
        public (bool IsEligible, string SacramentBadge) EvaluateSacramentClassEligibility(string? className, string? gradeLevel)
        {
            var normalizedClass = RemoveDiacritics(className ?? string.Empty).ToUpperInvariant();
            var normalizedGrade = RemoveDiacritics(gradeLevel ?? string.Empty).ToUpperInvariant();

            if (normalizedClass.Contains("RUOC LE 2") || normalizedClass.StartsWith("RL2") || normalizedClass.Contains("RL 2"))
                return (true, "Chuẩn bị Xưng Tội & Rước Lễ Lần Đầu (RL2)");

            if (normalizedClass.Contains("THEM SUC 3") || normalizedClass.StartsWith("TS3") || normalizedClass.Contains("TS 3"))
                return (true, "Chuẩn bị Lãnh Bí Tích Thêm Sức (TS3)");

            if (normalizedClass.Contains("BAO DONG 3") || normalizedClass.StartsWith("BD3") || normalizedClass.Contains("BD 3"))
                return (true, "Chuẩn bị Tuyên Hứa Bao Đồng (BĐ3)");

            if (normalizedClass.Contains("DU BI 4") || normalizedClass.StartsWith("DB4") || normalizedClass.Contains("DB 4") || normalizedGrade.Contains("DU BI"))
                return (true, "Chuẩn bị Tuyên Hứa Sống Đạo (DB4)");

            return (false, "Lớp thông thường (Không thuộc diện Chiến dịch Bí tích)");
        }

        public async Task<bool> CanUserManageClassCampaignAsync(ClaimsPrincipal user, int classRoomId)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return false;

            if (user.IsInRole(UserRole.Admin) ||
                user.IsInRole(UserRole.SpiritualDirector) ||
                user.IsInRole(UserRole.ExecutiveBoard))
            {
                return true;
            }

            var classRoom = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classRoomId);

            if (classRoom == null)
                return false;

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
            if (int.TryParse(userIdStr, out int userId))
            {
                return await _context.ClassTeachers
                    .AsNoTracking()
                    .AnyAsync(ct => ct.ClassRoomId == classRoomId && ct.UserId == userId);
            }

            return false;
        }

        public async Task<MassCampaignDashboardViewModel> BuildDashboardViewModelAsync(
            ClaimsPrincipal user,
            int? classId = null,
            long? campaignId = null)
        {
            var vm = new MassCampaignDashboardViewModel();

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent)
                ?? await _context.AcademicYears.AsNoTracking().OrderByDescending(y => y.Id).FirstOrDefaultAsync();

            if (currentYear == null)
                return vm;

            vm.AcademicYearId = currentYear.Id;
            vm.AcademicYearName = currentYear.Name;

            var allYearClasses = await _context.Classes
                .AsNoTracking()
                .Where(c => c.AcademicYearId == currentYear.Id)
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // Lọc danh sách lớp theo quyền của người dùng và chỉ lấy các lớp chuẩn bị Bí tích
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int currentUserId);
            bool isFullScope = user.IsInRole(UserRole.Admin) ||
                               user.IsInRole(UserRole.SpiritualDirector) ||
                               user.IsInRole(UserRole.ExecutiveBoard);

            var assignedClassIds = new HashSet<int>();
            if (!isFullScope && currentUserId > 0)
            {
                var teacherClassIds = await _context.ClassTeachers
                    .AsNoTracking()
                    .Where(ct => ct.UserId == currentUserId)
                    .Select(ct => ct.ClassRoomId)
                    .ToListAsync();
                foreach (var id in teacherClassIds) assignedClassIds.Add(id);
            }

            var managedGrade = user.FindFirst("ManagedGradeLevel")?.Value;

            var activeCampaignClassIds = await _context.MassCampaigns
                .AsNoTracking()
                .Where(mc => mc.AcademicYearId == currentYear.Id && mc.Status == "ACTIVE")
                .Select(mc => mc.ClassRoomId)
                .Distinct()
                .ToListAsync();
            var activeCampaignSet = activeCampaignClassIds.ToHashSet();

            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.AcademicYearId == currentYear.Id)
                .GroupBy(e => e.ClassRoomId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            foreach (var cls in allYearClasses)
            {
                var (isEligible, badge) = EvaluateSacramentClassEligibility(cls.Name, cls.GradeLevel);
                if (!isEligible)
                    continue;

                bool hasAccess = isFullScope ||
                                 assignedClassIds.Contains(cls.Id) ||
                                 (user.IsInRole(UserRole.BranchHead) &&
                                  !string.IsNullOrWhiteSpace(managedGrade) &&
                                  string.Equals(managedGrade.Trim(), cls.GradeLevel.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!hasAccess)
                    continue;

                vm.AvailableClasses.Add(new EligibleCampaignClassItemDto
                {
                    ClassId = cls.Id,
                    ClassName = cls.Name,
                    GradeLevel = cls.GradeLevel,
                    SacramentLabel = badge,
                    StudentCount = enrollmentCounts.TryGetValue(cls.Id, out int cnt) ? cnt : 0,
                    HasActiveCampaign = activeCampaignSet.Contains(cls.Id)
                });
            }

            if (!vm.AvailableClasses.Any())
                return vm;

            var chosenClass = classId.HasValue
                ? vm.AvailableClasses.FirstOrDefault(c => c.ClassId == classId.Value) ?? vm.AvailableClasses.First()
                : vm.AvailableClasses.First();

            vm.SelectedClassId = chosenClass.ClassId;
            vm.SelectedClassName = chosenClass.ClassName;
            vm.SelectedGradeLevel = chosenClass.GradeLevel;
            vm.IsEligibleSacramentClass = true;
            vm.SacramentClassBadge = chosenClass.SacramentLabel;
            vm.CanManageCampaign = await CanUserManageClassCampaignAsync(user, chosenClass.ClassId);

            var campaigns = await _context.MassCampaigns
                .AsNoTracking()
                .Where(c => c.ClassRoomId == chosenClass.ClassId && c.AcademicYearId == currentYear.Id)
                .OrderByDescending(c => c.Status == "ACTIVE")
                .ThenByDescending(c => c.StartDate)
                .ToListAsync();

            vm.ClassCampaigns = campaigns.Select(c => new CampaignSummaryItemDto
            {
                CampaignId = c.Id,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                TargetDaysCount = c.TargetDaysCount,
                Status = c.Status,
                CreatedBy = c.CreatedBy
            }).ToList();

            var selectedCampaign = campaignId.HasValue
                ? campaigns.FirstOrDefault(c => c.Id == campaignId.Value) ?? campaigns.FirstOrDefault()
                : campaigns.FirstOrDefault();

            if (selectedCampaign != null)
            {
                vm.ActiveCampaignMatrix = await BuildCampaignMatrixInternalAsync(selectedCampaign, chosenClass.ClassName);
            }

            return vm;
        }

        public async Task<(bool Success, string Message, MassCampaign? Campaign)> CreateCampaignAsync(
            CreateMassCampaignRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            if (!await CanUserManageClassCampaignAsync(user, dto.ClassRoomId))
                return (false, "Bạn không có quyền thiết lập chiến dịch cho lớp này.", null);

            var classRoom = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ClassRoomId);

            if (classRoom == null)
                return (false, "Không tìm thấy thông tin lớp học.", null);

            var (isEligible, _) = EvaluateSacramentClassEligibility(classRoom.Name, classRoom.GradeLevel);
            if (!isEligible)
            {
                return (false, $"Lớp {classRoom.Name} không thuộc nhóm lớp chuẩn bị lãnh nhận Bí tích / Tuyên hứa (RL2, TS3, BĐ3, DB4).", null);
            }

            var startDate = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(dto.EndDate.Date, DateTimeKind.Utc);

            if (endDate < startDate)
                return (false, "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.", null);

            var totalSpanDays = (endDate - startDate).TotalDays + 1;
            if (totalSpanDays < 7 || totalSpanDays > 45)
                return (false, "Chiến dịch đi lễ 1 tháng cần có độ dài từ 7 đến 45 ngày (khuyến nghị 28 - 31 ngày).", null);

            var validDays = (dto.ApplicableDays ?? new List<int> { 0, 1, 2, 3, 4, 5, 6 })
                .Where(d => d >= 0 && d <= 6)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (!validDays.Any())
                validDays = new List<int> { 0, 1, 2, 3, 4, 5, 6 };

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";

            var campaign = new MassCampaign
            {
                AcademicYearId = classRoom.AcademicYearId,
                ClassRoomId = classRoom.Id,
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                ApplicableDaysCsv = string.Join(",", validDays),
                TargetDaysCount = dto.TargetDaysCount,
                Status = "ACTIVE",
                CreatedBy = operatorName,
                CreatedAt = DateTime.UtcNow
            };

            _context.MassCampaigns.Add(campaign);

            //_context.AuditLogs.Add(new AuditLog
            //{
            //    Username = user.Identity?.Name ?? operatorName,
            //    Action = "CREATE_MASS_CAMPAIGN",
            //    Details = $"Khởi tạo chiến dịch đi lễ '{campaign.Title}' cho lớp {classRoom.Name} ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}, chỉ tiêu {dto.TargetDaysCount} ngày).",
            //    IpAddress = ipAddress,
            //    Timestamp = DateTime.UtcNow
            //});

            await _context.SaveChangesAsync();
            return (true, $"Đã khởi tạo chiến dịch \"{campaign.Title}\" thành công cho lớp {classRoom.Name}.", campaign);
        }

        public async Task<(bool Success, string Message)> UpdateCampaignConfigAsync(
            UpdateMassCampaignConfigDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            var campaign = await _context.MassCampaigns
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == dto.CampaignId);

            if (campaign == null)
                return (false, "Không tìm thấy chiến dịch đi lễ.");

            if (!await CanUserManageClassCampaignAsync(user, campaign.ClassRoomId))
                return (false, "Bạn không có quyền chỉnh sửa chiến dịch của lớp này.");

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";
            var allowedStatuses = new[] { "ACTIVE", "COMPLETED", "CANCELLED" };
            var newStatus = allowedStatuses.Contains(dto.Status.ToUpperInvariant())
                ? dto.Status.ToUpperInvariant()
                : campaign.Status;

            campaign.Title = dto.Title.Trim();
            campaign.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            campaign.TargetDaysCount = dto.TargetDaysCount;
            campaign.Status = newStatus;
            campaign.UpdatedBy = operatorName;
            campaign.UpdatedAt = DateTime.UtcNow;

            //_context.AuditLogs.Add(new AuditLog
            //{
            //    Username = user.Identity?.Name ?? operatorName,
            //    Action = "UPDATE_MASS_CAMPAIGN",
            //    Details = $"Cập nhật cấu hình chiến dịch #{campaign.Id} ({campaign.Title}) lớp {campaign.ClassRoom?.ClassName}: Chỉ tiêu={dto.TargetDaysCount}, Trạng thái={newStatus}.",
            //    IpAddress = ipAddress,
            //    Timestamp = DateTime.UtcNow
            //});

            await _context.SaveChangesAsync();
            return (true, "Đã cập nhật cấu hình chiến dịch đi lễ.");
        }

        public async Task<(bool Success, string Message, StudentCampaignRowDto? UpdatedRow)> UpdateSingleCellAsync(
            UpdateCampaignCellRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            var campaign = await _context.MassCampaigns
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == dto.CampaignId);

            if (campaign == null)
                return (false, "Không tìm thấy chiến dịch đi lễ.", null);

            if (!await CanUserManageClassCampaignAsync(user, campaign.ClassRoomId))
                return (false, "Bạn không có quyền chỉnh sửa điểm danh của lớp này.", null);

            if (!DateTime.TryParseExact(dto.DateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                return (false, "Định dạng ngày không hợp lệ.", null);

            var targetDateUtc = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            if (targetDateUtc < campaign.StartDate.Date || targetDateUtc > campaign.EndDate.Date)
                return (false, "Ngày điểm danh nằm ngoài phạm vi của chiến dịch.", null);

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId && e.ClassRoomId == campaign.ClassRoomId);

            if (enrollment == null || enrollment.Student == null)
                return (false, "Học sinh không thuộc lớp của chiến dịch này.", null);

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";
            var normalizedStatus = dto.Status.Trim().ToUpperInvariant();

            var existingRecord = await _context.MassCampaignRecords
                .FirstOrDefaultAsync(r => r.CampaignId == campaign.Id
                                       && r.EnrollmentId == enrollment.Id
                                       && r.AttendanceDate == targetDateUtc);

            string previousStatus = existingRecord?.Status ?? "NONE";

            if (normalizedStatus == "CLEAR" || normalizedStatus == "NONE")
            {
                if (existingRecord != null)
                {
                    _context.MassCampaignRecords.Remove(existingRecord);
                }
            }
            else
            {
                var validStatuses = new[] { "PRESENT", "LATE", "EXCUSED", "ABSENT" };
                if (!validStatuses.Contains(normalizedStatus))
                    return (false, "Trạng thái điểm danh không hợp lệ.", null);

                if (existingRecord == null)
                {
                    existingRecord = new MassCampaignRecord
                    {
                        CampaignId = campaign.Id,
                        StudentId = enrollment.StudentId,
                        EnrollmentId = enrollment.Id,
                        AttendanceDate = targetDateUtc,
                        Status = normalizedStatus,
                        CheckInMethod = "MANUAL_GRID",
                        CheckInTime = DateTime.UtcNow,
                        Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                        LastModifiedBy = operatorName,
                        LastModifiedAt = DateTime.UtcNow
                    };
                    _context.MassCampaignRecords.Add(existingRecord);
                }
                else
                {
                    existingRecord.Status = normalizedStatus;
                    existingRecord.CheckInMethod = "MANUAL_GRID";
                    if (!string.IsNullOrWhiteSpace(dto.Note))
                    {
                        existingRecord.Note = dto.Note.Trim();
                    }
                    existingRecord.LastModifiedBy = operatorName;
                    existingRecord.LastModifiedAt = DateTime.UtcNow;
                }
            }

            //_context.AuditLogs.Add(new AuditLog
            //{
            //    Username = user.Identity?.Name ?? operatorName,
            //    Action = "EDIT_CAMPAIGN_ATTENDANCE",
            //    Details = $"Chỉnh sửa điểm danh chiến dịch '{campaign.Title}' ngày {targetDateUtc:dd/MM/yyyy} của thiếu nhi {enrollment.Student.StudentCode} ({enrollment.Student.FullName}): {previousStatus} -> {normalizedStatus}. Ghi chú: {dto.Note ?? "Không"}",
            //    IpAddress = ipAddress,
            //    Timestamp = DateTime.UtcNow
            //});

            await _context.SaveChangesAsync();

            var updatedMatrix = await BuildCampaignMatrixInternalAsync(campaign, campaign.ClassRoom?.Name ?? "");
            var updatedRow = updatedMatrix.StudentRows.FirstOrDefault(r => r.EnrollmentId == enrollment.Id);

            return (true, $"Đã cập nhật ngày {targetDateUtc:dd/MM} cho em {enrollment.Student.FirstName} {enrollment.Student.LastName}.", updatedRow);
        }

        public async Task<(bool Success, string Message, int UpdatedCount)> BulkMarkDayAsync(
            BulkMarkCampaignDayRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            var campaign = await _context.MassCampaigns
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == dto.CampaignId);

            if (campaign == null)
                return (false, "Không tìm thấy chiến dịch đi lễ.", 0);

            if (!await CanUserManageClassCampaignAsync(user, campaign.ClassRoomId))
                return (false, "Bạn không có quyền điểm danh cho lớp này.", 0);

            if (!DateTime.TryParseExact(dto.DateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                return (false, "Ngày điểm danh không hợp lệ.", 0);

            var targetDateUtc = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            if (targetDateUtc < campaign.StartDate.Date || targetDateUtc > campaign.EndDate.Date)
                return (false, "Ngày đã chọn nằm ngoài thời gian chiến dịch.", 0);

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassRoomId == campaign.ClassRoomId && e.AcademicYearId == campaign.AcademicYearId)
                .ToListAsync();

            var existingRecords = await _context.MassCampaignRecords
                .Where(r => r.CampaignId == campaign.Id && r.AttendanceDate == targetDateUtc)
                .ToDictionaryAsync(r => r.EnrollmentId);

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";
            var normalizedStatus = dto.Status.Trim().ToUpperInvariant();
            int count = 0;

            foreach (var env in enrollments)
            {
                if (existingRecords.TryGetValue(env.Id, out var rec))
                {
                    if (dto.OverwriteExisting)
                    {
                        rec.Status = normalizedStatus;
                        rec.CheckInMethod = "BULK_DAY";
                        rec.LastModifiedBy = operatorName;
                        rec.LastModifiedAt = DateTime.UtcNow;
                        count++;
                    }
                }
                else
                {
                    _context.MassCampaignRecords.Add(new MassCampaignRecord
                    {
                        CampaignId = campaign.Id,
                        StudentId = env.StudentId,
                        EnrollmentId = env.Id,
                        AttendanceDate = targetDateUtc,
                        Status = normalizedStatus,
                        CheckInMethod = "BULK_DAY",
                        CheckInTime = DateTime.UtcNow,
                        LastModifiedBy = operatorName,
                        LastModifiedAt = DateTime.UtcNow
                    });
                    count++;
                }
            }

            //_context.AuditLogs.Add(new AuditLog
            //{
            //    Username = user.Identity?.Name ?? operatorName,
            //    Action = "BULK_CAMPAIGN_ATTENDANCE",
            //    Details = $"Điểm danh hàng loạt ngày {targetDateUtc:dd/MM/yyyy} chiến dịch '{campaign.Title}' lớp {campaign.ClassRoom?.ClassName} với trạng thái {normalizedStatus} ({count} học sinh).",
            //    IpAddress = ipAddress,
            //    Timestamp = DateTime.UtcNow
            //});

            await _context.SaveChangesAsync();
            return (true, $"Đã ghi nhận trạng thái {normalizedStatus} cho {count} thiếu nhi trong ngày {targetDateUtc:dd/MM/yyyy}.", count);
        }

        public async Task<(bool Success, string Message, object? StudentInfo)> RecordQrScanAsync(
            CampaignQrScanRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            var campaign = await _context.MassCampaigns
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == dto.CampaignId);

            if (campaign == null || campaign.Status != "ACTIVE")
                return (false, "Chiến dịch không tồn tại hoặc đã kết thúc.", null);

            if (!await CanUserManageClassCampaignAsync(user, campaign.ClassRoomId))
                return (false, "Bạn không có quyền quét QR cho chiến dịch lớp này.", null);

            DateTime scanDateLocal = DateTime.UtcNow.AddHours(7).Date;
            if (!string.IsNullOrWhiteSpace(dto.DateIso) &&
                DateTime.TryParseExact(dto.DateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime customDate))
            {
                scanDateLocal = customDate.Date;
            }

            var targetDateUtc = DateTime.SpecifyKind(scanDateLocal, DateTimeKind.Utc);
            if (targetDateUtc < campaign.StartDate.Date || targetDateUtc > campaign.EndDate.Date)
                return (false, $"Ngày {targetDateUtc:dd/MM/yyyy} không nằm trong khung thời gian chiến dịch ({campaign.StartDate:dd/MM} - {campaign.EndDate:dd/MM}).", null);

            var cleanCode = dto.StudentCode.Trim().ToUpperInvariant();
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.ClassRoomId == campaign.ClassRoomId
                                       && e.AcademicYearId == campaign.AcademicYearId
                                       && e.Student != null
                                       && e.Student.StudentCode.ToUpper() == cleanCode);

            if (enrollment?.Student == null)
                return (false, $"Mã thiếu nhi '{cleanCode}' không thuộc lớp {campaign.ClassRoom?.Name}.", null);

            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";

            var existing = await _context.MassCampaignRecords
                .FirstOrDefaultAsync(r => r.CampaignId == campaign.Id
                                       && r.EnrollmentId == enrollment.Id
                                       && r.AttendanceDate == targetDateUtc);

            if (existing != null)
            {
                existing.Status = "PRESENT";
                existing.CheckInMethod = "QR_SCAN";
                existing.CheckInTime = DateTime.UtcNow;
                existing.LastModifiedBy = operatorName;
                existing.LastModifiedAt = DateTime.UtcNow;
            }
            else
            {
                _context.MassCampaignRecords.Add(new MassCampaignRecord
                {
                    CampaignId = campaign.Id,
                    StudentId = enrollment.StudentId,
                    EnrollmentId = enrollment.Id,
                    AttendanceDate = targetDateUtc,
                    Status = "PRESENT",
                    CheckInMethod = "QR_SCAN",
                    CheckInTime = DateTime.UtcNow,
                    LastModifiedBy = operatorName,
                    LastModifiedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return (true, $"Điểm danh chiến dịch thành công: {enrollment.Student.ChristianName} {enrollment.Student.FirstName} {enrollment.Student.LastName}", new
            {
                studentCode = enrollment.Student.StudentCode,
                fullName = $"{enrollment.Student.ChristianName} {enrollment.Student.FirstName} {enrollment.Student.LastName}".Trim(),
                enrollmentId = enrollment.Id,
                dateIso = targetDateUtc.ToString("yyyy-MM-dd"),
                status = "PRESENT"
            });
        }

        public async Task<byte[]> ExportCampaignMatrixToExcelAsync(long campaignId, ClaimsPrincipal user)
        {
            var campaign = await _context.MassCampaigns
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == campaignId)
                ?? throw new KeyNotFoundException("Không tìm thấy chiến dịch đi lễ.");

            if (!await CanUserManageClassCampaignAsync(user, campaign.ClassRoomId))
                throw new UnauthorizedAccessException("Bạn không có quyền xuất dữ liệu chiến dịch của lớp này.");

            var matrix = await BuildCampaignMatrixInternalAsync(campaign, campaign.ClassRoom?.Name ?? "");

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("ChienDichDiLe30Ngay");

            ws.Cell(1, 1).Value = $"BẢNG TỔNG KẾT CHIẾN DỊCH ĐI LỄ 1 THÁNG - LỚP {matrix.ClassName.ToUpperInvariant()}";
            ws.Range(1, 1, 1, 9 + matrix.DateColumns.Count).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(13)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Chiến dịch: {matrix.Title} | Thời gian: {matrix.StartDate:dd/MM/yyyy} - {matrix.EndDate:dd/MM/yyyy} | Chỉ tiêu: {matrix.TargetDaysCount}/{matrix.TotalApplicableDays} ngày";
            ws.Range(2, 1, 2, 9 + matrix.DateColumns.Count).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int headerRow = 4;
            ws.Cell(headerRow, 1).Value = "STT";
            ws.Cell(headerRow, 2).Value = "Mã TN";
            ws.Cell(headerRow, 3).Value = "Tên Thánh";
            ws.Cell(headerRow, 4).Value = "Họ và Tên";

            int colIdx = 5;
            foreach (var col in matrix.DateColumns)
            {
                ws.Cell(headerRow, colIdx).Value = $"{col.DayNumberText}\n({col.DayOfWeekShort})";
                ws.Column(colIdx).Width = 6.5;
                colIdx++;
            }

            ws.Cell(headerRow, colIdx++).Value = "Có mặt";
            ws.Cell(headerRow, colIdx++).Value = "Đi trễ";
            ws.Cell(headerRow, colIdx++).Value = "Có phép";
            ws.Cell(headerRow, colIdx++).Value = "Chuỗi Max";
            ws.Cell(headerRow, colIdx).Value = "Kết quả";

            var headerRange = ws.Range(headerRow, 1, headerRow, colIdx);
            headerRange.Style.Font.SetBold(true);
            headerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1e40af"));
            headerRange.Style.Font.SetFontColor(XLColor.White);
            headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            headerRange.Style.Alignment.SetWrapText(true);

            int currentRow = headerRow + 1;
            int stt = 1;
            foreach (var row in matrix.StudentRows)
            {
                ws.Cell(currentRow, 1).Value = stt++;
                ws.Cell(currentRow, 2).Value = SanitizeExcelValue(row.StudentCode);
                ws.Cell(currentRow, 3).Value = SanitizeExcelValue(row.ChristianName);
                ws.Cell(currentRow, 4).Value = SanitizeExcelValue(row.FullName);

                int c = 5;
                foreach (var dateCol in matrix.DateColumns)
                {
                    var key = dateCol.Date.ToString("yyyy-MM-dd");
                    string symbol = "";
                    if (row.DailyCells.TryGetValue(key, out var cell))
                    {
                        symbol = cell.Status switch
                        {
                            "PRESENT" => "✔",
                            "LATE" => "T",
                            "EXCUSED" => "P",
                            "ABSENT" => "V",
                            _ => ""
                        };
                    }
                    ws.Cell(currentRow, c).Value = symbol;
                    ws.Cell(currentRow, c).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    c++;
                }

                ws.Cell(currentRow, c++).Value = row.PresentDays;
                ws.Cell(currentRow, c++).Value = row.LateDays;
                ws.Cell(currentRow, c++).Value = row.ExcusedDays;
                ws.Cell(currentRow, c++).Value = row.MaxStreak;
                ws.Cell(currentRow, c).Value = row.IsTargetMet ? "ĐẠT CHỈ TIÊU" : "Chưa đạt";

                currentRow++;
            }

            ws.Columns(1, 4).AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task<CampaignDetailMatrixDto> BuildCampaignMatrixInternalAsync(MassCampaign campaign, string className)
        {
            var matrix = new CampaignDetailMatrixDto
            {
                CampaignId = campaign.Id,
                ClassRoomId = campaign.ClassRoomId,
                ClassName = className,
                Title = campaign.Title,
                Description = campaign.Description,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                ApplicableDaysCsv = campaign.ApplicableDaysCsv,
                TargetDaysCount = campaign.TargetDaysCount,
                Status = campaign.Status,
                CreatedBy = campaign.CreatedBy
            };

            var allowedDays = campaign.ApplicableDaysCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out int d) ? d : -1)
                .Where(d => d >= 0 && d <= 6)
                .ToHashSet();

            if (!allowedDays.Any())
                allowedDays = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 };

            var todayVn = DateTime.UtcNow.AddHours(7).Date;
            for (var dt = campaign.StartDate.Date; dt <= campaign.EndDate.Date; dt = dt.AddDays(1))
            {
                if (!allowedDays.Contains((int)dt.DayOfWeek))
                    continue;

                matrix.DateColumns.Add(new CampaignDateColumnDto
                {
                    Date = dt,
                    DayOfWeekShort = GetVietnameseDayShort(dt.DayOfWeek),
                    IsToday = dt == todayVn,
                    IsFuture = dt > todayVn
                });
            }

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == campaign.ClassRoomId
                         && e.AcademicYearId == campaign.AcademicYearId
                         && e.Student != null
                         && !e.Student.IsDeleted)
                .OrderBy(e => e.Student!.StudentCode)
                .ToListAsync();

            var records = await _context.MassCampaignRecords
                .AsNoTracking()
                .Where(r => r.CampaignId == campaign.Id)
                .ToListAsync();

            var recordsLookup = records
                .GroupBy(r => r.EnrollmentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(r => r.AttendanceDate.Date.ToString("yyyy-MM-dd")));

            var dayPresentCounters = matrix.DateColumns.ToDictionary(d => d.Date.ToString("yyyy-MM-dd"), _ => 0);

            foreach (var env in enrollments)
            {
                var student = env.Student!;
                var row = new StudentCampaignRowDto
                {
                    StudentId = student.Id,
                    EnrollmentId = env.Id,
                    StudentCode = student.StudentCode,
                    ChristianName = student.ChristianName ?? string.Empty,
                    FullName = student.FirstName + " " + student.LastName,
                };

                recordsLookup.TryGetValue(env.Id, out var studentRecMap);

                int runningStreak = 0;
                int bestStreak = 0;
                int currentActiveStreak = 0;

                foreach (var dateCol in matrix.DateColumns)
                {
                    var key = dateCol.Date.ToString("yyyy-MM-dd");
                    if (studentRecMap != null && studentRecMap.TryGetValue(key, out var rec))
                    {
                        row.DailyCells[key] = new CampaignCellStatusDto
                        {
                            RecordId = rec.Id,
                            Status = rec.Status,
                            CheckInMethod = rec.CheckInMethod,
                            Note = rec.Note,
                            LastModifiedBy = rec.LastModifiedBy,
                            LastModifiedAt = rec.LastModifiedAt
                        };

                        if (rec.Status == "PRESENT")
                        {
                            row.PresentDays++;
                            runningStreak++;
                            dayPresentCounters[key]++;
                        }
                        else if (rec.Status == "LATE")
                        {
                            row.LateDays++;
                            runningStreak++;
                            dayPresentCounters[key]++;
                        }
                        else if (rec.Status == "EXCUSED")
                        {
                            row.ExcusedDays++;
                            // Nghỉ có phép giữ nguyên chuỗi hiện tại nhưng không cộng thêm
                        }
                        else
                        {
                            row.AbsentDays++;
                            runningStreak = 0;
                        }
                    }
                    else
                    {
                        row.DailyCells[key] = new CampaignCellStatusDto { Status = "NONE" };
                        if (!dateCol.IsFuture && !dateCol.IsToday)
                        {
                            runningStreak = 0;
                        }
                    }

                    if (runningStreak > bestStreak)
                        bestStreak = runningStreak;

                    if (!dateCol.IsFuture)
                        currentActiveStreak = runningStreak;
                }

                row.CurrentStreak = currentActiveStreak;
                row.MaxStreak = bestStreak;
                row.CompletionPercentage = campaign.TargetDaysCount > 0
                    ? Math.Min(100.0, Math.Round((double)row.TotalAttendedDays * 100.0 / campaign.TargetDaysCount, 1))
                    : 0.0;
                row.IsTargetMet = row.TotalAttendedDays >= campaign.TargetDaysCount;

                matrix.StudentRows.Add(row);
            }

            foreach (var dateCol in matrix.DateColumns)
            {
                var key = dateCol.Date.ToString("yyyy-MM-dd");
                dateCol.PresentCount = dayPresentCounters.TryGetValue(key, out int cnt) ? cnt : 0;
            }

            return matrix;
        }

        private static string GetVietnameseDayShort(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Sunday => "CN",
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            _ => ""
        };

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        private static string SanitizeExcelValue(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.StartsWith("=") || input.StartsWith("+") || input.StartsWith("-") || input.StartsWith("@"))
                return "'" + input;
            return input;
        }
    }
}