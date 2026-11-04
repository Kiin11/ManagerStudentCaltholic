using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagerStudentCaltholic.Services
{
    public class StaffStudentLookupService : IStaffStudentLookupService
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeCalcService;
        private readonly ILogger<StaffStudentLookupService> _logger;

        public StaffStudentLookupService(
            ParishDbContext context,
            IGradeCalculationService gradeCalcService,
            ILogger<StaffStudentLookupService> logger)
        {
            _context = context;
            _gradeCalcService = gradeCalcService;
            _logger = logger;
        }

        public async Task<List<LookupViewOption>> GetViewOptionsAsync()
        {
            var options = await _context.LookupViewOptions
                .AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.SortOrder)
                .ToListAsync();

            if (!options.Any())
            {
                // Fallback default 4 options if database not yet migrated
                return new List<LookupViewOption>
                {
                    new() { Id = 1, OptionCode = "OVERVIEW", DisplayName = "Tổng quan", Description = "Hồ sơ cá nhân, phân bổ lớp học & tóm tắt tình trạng hiện tại", IconClass = "bi-person-badge", BadgeColor = "primary", SortOrder = 1, IsActive = true },
                    new() { Id = 2, OptionCode = "ATTENDANCE_DETAIL", DisplayName = "Chuyên cần & Chiến dịch", Description = "Lễ Chúa Nhật & Thứ Năm gần nhất, giờ học & Chiến dịch đi lễ 1 tháng", IconClass = "bi-calendar-check", BadgeColor = "success", SortOrder = 2, IsActive = true },
                    new() { Id = 3, OptionCode = "ACADEMIC_2Y_SACRAMENT", DisplayName = "Lịch sử 2 năm & Bí tích", Description = "ĐTB cả năm, tỷ lệ chuyên cần 2 năm trước, bảng điểm & mốc Bí tích", IconClass = "bi-journal-bookmark", BadgeColor = "warning", SortOrder = 3, IsActive = true },
                    new() { Id = 4, OptionCode = "PARENT_CONTACT", DisplayName = "Liên hệ phụ huynh", Description = "Thông tin liên hệ phụ huynh, địa chỉ & mẫu soạn tin nhắn Zalo/SMS", IconClass = "bi-telephone", BadgeColor = "info", SortOrder = 4, IsActive = true }
                };
            }

            return options;
        }

        public async Task<List<StaffStudentQuickSearchResultDto>> SearchStudentsQuickAsync(string keyword, int limit = 8)
        {
            var results = new List<StaffStudentQuickSearchResultDto>();
            if (string.IsNullOrWhiteSpace(keyword))
                return results;

            var cleanKeyword = keyword.Trim();
            var lowerKeyword = cleanKeyword.ToLower();
            var noDiacriticsKeyword = VietnameseStringHelper.RemoveDiacritics(cleanKeyword);

            // 1. Tìm trong bảng StudentCodeHistories theo mã cũ (OriginalCode hoặc ArchivedYearCode)
            var matchedHistories = await _context.StudentCodeHistories
                .AsNoTracking()
                .Where(h => h.OriginalCode.ToLower().Contains(lowerKeyword) ||
                            h.ArchivedYearCode.ToLower().Contains(lowerKeyword) ||
                            h.NewCode.ToLower().Contains(lowerKeyword))
                .OrderByDescending(h => h.ChangedAt)
                .Take(limit)
                .ToListAsync();

            var historyStudentIds = matchedHistories.Select(h => h.StudentId).Distinct().ToList();

            // 2. Tìm học sinh theo Tên, Tên Thánh, Mã hiện tại hoặc từ danh sách lịch sử
            var currentAcademicYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            var query = _context.Students
                .AsNoTracking()
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.ClassRoom)
                        .ThenInclude(c => c.AcademicYear)
                .Where(s => s.IsActive && !s.IsDeleted &&
                    (s.StudentCode.ToLower().Contains(lowerKeyword) ||
                     s.ChristianName.ToLower().Contains(lowerKeyword) ||
                     s.FirstName.ToLower().Contains(lowerKeyword) ||
                     s.LastName.ToLower().Contains(lowerKeyword) ||
                     (s.LastName + " " + s.FirstName).ToLower().Contains(lowerKeyword) ||
                     (s.ChristianName + " " + s.LastName + " " + s.FirstName).ToLower().Contains(lowerKeyword) ||
                     historyStudentIds.Contains(s.Id)));

            var matchedStudents = await query.Take(limit * 2).ToListAsync();

            // Lọc thêm theo không dấu nếu cần
            if (!string.IsNullOrWhiteSpace(noDiacriticsKeyword))
            {
                var candidates = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.ClassRoom)
                            .ThenInclude(c => c.AcademicYear)
                    .Where(s => s.IsActive && !s.IsDeleted)
                    .Take(200)
                    .ToListAsync();

                foreach (var c in candidates)
                {
                    if (matchedStudents.All(s => s.Id != c.Id))
                    {
                        var fullName = $"{c.ChristianName} {c.LastName} {c.FirstName}";
                        var noDiacriticsName = VietnameseStringHelper.RemoveDiacritics(fullName);
                        if (noDiacriticsName.Contains(noDiacriticsKeyword))
                        {
                            matchedStudents.Add(c);
                        }
                    }
                }
            }

            foreach (var student in matchedStudents.Take(limit))
            {
                var historyMatch = matchedHistories.FirstOrDefault(h => h.StudentId == student.Id);
                var isHistorical = historyMatch != null &&
                    !string.Equals(student.StudentCode, cleanKeyword, StringComparison.OrdinalIgnoreCase);

                var currentEnrollment = student.Enrollments
                    .OrderByDescending(e => e.ClassRoom?.AcademicYear?.IsCurrent == true ? 1 : 0)
                    .ThenByDescending(e => e.EnrolledAt)
                    .FirstOrDefault();

                results.Add(new StaffStudentQuickSearchResultDto
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    ChristianName = student.ChristianName,
                    FullName = $"{student.FirstName} {student.LastName}".Trim(),
                    Gender = student.Gender,
                    DateOfBirth = student.DateOfBirth,
                    CurrentClassName = currentEnrollment?.ClassRoom?.Name,
                    CurrentAcademicYearName = currentEnrollment?.ClassRoom?.AcademicYear?.Name ?? currentAcademicYear?.Name,
                    IsHistoricalMatch = isHistorical,
                    MatchedHistoricalCode = isHistorical ? (historyMatch?.OriginalCode ?? historyMatch?.ArchivedYearCode) : null
                });
            }

            return results;
        }

        public async Task<StaffStudent360DetailDto?> GetStudent360DetailAsync(long studentId, string selectedOption = "OVERVIEW")
        {
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.ClassRoom)
                        .ThenInclude(c => c.AcademicYear)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.GradeRecords)
                        .ThenInclude(g => g.GradeConfiguration)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
                return null;

            var currentAcademicYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            // Tìm niên khóa hiện tại và Enrollment hiện tại
            var currentEnrollment = student.Enrollments
                .OrderByDescending(e => e.ClassRoom?.AcademicYear?.IsCurrent == true ? 1 : 0)
                .ThenByDescending(e => e.EnrolledAt)
                .FirstOrDefault();

            var currentYearId = currentEnrollment?.ClassRoom?.AcademicYearId ?? currentAcademicYear?.Id ?? 0;
            var currentYearName = currentEnrollment?.ClassRoom?.AcademicYear?.Name ?? currentAcademicYear?.Name ?? "Niên khóa hiện hành";

            var dto = new StaffStudent360DetailDto
            {
                ActiveOption = string.IsNullOrWhiteSpace(selectedOption) ? "OVERVIEW" : selectedOption.ToUpperInvariant()
            };

            // 1. Basic Info
            dto.BasicInfo = new StudentBasicInfoDto
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                ChristianName = student.ChristianName,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Gender = student.Gender,
                DateOfBirth = student.DateOfBirth,
                ParentPhone = student.ParentPhone,
                IsActive = student.IsActive,
                CurrentClassName = currentEnrollment?.ClassRoom?.Name,
                CurrentGradeLevel = currentEnrollment?.ClassRoom?.GradeLevel,
                CurrentRoomName = currentEnrollment?.ClassRoom?.RoomName,
                CurrentShift = currentEnrollment?.ClassRoom?.Shift,
                CurrentAcademicYearName = currentYearName,
                CurrentEnrollmentId = currentEnrollment?.Id
            };

            // 2. Attendance & Mass Campaign
            await PopulateAttendanceAndCampaignAsync(dto, student.Id, currentEnrollment);

            // 3. Academic & Past 2 Years
            await PopulateAcademicHistoryAsync(dto, student.Id, currentYearId, currentEnrollment);

            // 4. Sacraments
            await PopulateSacramentsAsync(dto, student);

            // 5. Parent Contact & Templates
            PopulateParentContact(dto, student, currentEnrollment?.ClassRoom?.Name);

            return dto;
        }

        private async Task PopulateAttendanceAndCampaignAsync(
            StaffStudent360DetailDto dto,
            long studentId,
            Enrollment? currentEnrollment)
        {
            var attendanceOverview = new AttendanceOverviewDto();

            if (currentEnrollment != null)
            {
                var attendances = await _context.Attendances
                    .AsNoTracking()
                    .Where(a => a.EnrollmentId == currentEnrollment.Id)
                    .OrderByDescending(a => a.AttendanceDate)
                    .ToListAsync();

                // Lễ Chúa Nhật gần nhất
                var latestSunday = attendances.FirstOrDefault(a => a.DayOfWeek == DayOfWeek.Sunday);
                if (latestSunday != null)
                {
                    attendanceOverview.LatestSunday = MapAttendanceItem(latestSunday, "MASS_SUNDAY");
                }

                // Lễ Thứ Năm gần nhất
                var latestThursday = attendances.FirstOrDefault(a => a.DayOfWeek == DayOfWeek.Thursday);
                if (latestThursday != null)
                {
                    attendanceOverview.LatestThursday = MapAttendanceItem(latestThursday, "MASS_THURSDAY");
                }

                // Giờ học Giáo lý gần nhất
                var latestClass = attendances.FirstOrDefault(a => a.ClassAttended || a.ClassCheckInTime != null || a.ClassStatus != "ABSENT_UNPERMITTED")
                                  ?? attendances.FirstOrDefault();
                if (latestClass != null)
                {
                    attendanceOverview.LatestClassSession = new RecentAttendanceItemDto
                    {
                        AttendanceDate = latestClass.AttendanceDate,
                        IsAttended = latestClass.ClassAttended,
                        Status = latestClass.ClassStatus,
                        StatusDisplay = MapClassStatusDisplay(latestClass.ClassStatus),
                        StatusBadgeColor = MapClassStatusBadge(latestClass.ClassStatus),
                        CheckInTime = latestClass.ClassCheckInTime,
                        SessionType = "CLASS_CATECHISM"
                    };
                }

                // Tổng kết Chuyên cần cả năm
                var massCount = attendances.Count;
                var massAttended = attendances.Count(a => a.AttendedMass);
                attendanceOverview.TotalMassAttendedCurrentYear = massAttended;
                attendanceOverview.TotalMassMissedCurrentYear = massCount - massAttended;
                attendanceOverview.MassAttendanceRatePercent = massCount > 0 ? Math.Round((double)massAttended / massCount * 100, 1) : 100.0;

                var classCount = attendances.Count;
                var classAttended = attendances.Count(a => a.ClassAttended);
                attendanceOverview.TotalClassAttendedCurrentYear = classAttended;
                attendanceOverview.TotalClassMissedCurrentYear = classCount - classAttended;
                attendanceOverview.ClassAttendanceRatePercent = classCount > 0 ? Math.Round((double)classAttended / classCount * 100, 1) : 100.0;
            }

            // Chiến dịch đi lễ 1 tháng
            if (currentEnrollment != null)
            {
                var campaign = await _context.MassCampaigns
                    .AsNoTracking()
                    .Where(c => c.ClassRoomId == currentEnrollment.ClassRoomId && !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (campaign != null)
                {
                    var records = await _context.MassCampaignRecords
                        .AsNoTracking()
                        .Where(r => r.CampaignId == campaign.Id && r.StudentId == studentId)
                        .OrderBy(r => r.AttendanceDate)
                        .ToListAsync();

                    var attendedRecords = records.Where(r => r.Status == "PRESENT" || r.Status == "LATE").ToList();
                    var attendedDays = attendedRecords.Count;

                    // Tính Streak liên tục tính đến ngày gần nhất
                    int streak = 0;
                    var sortedDates = attendedRecords.Select(r => r.AttendanceDate.Date).Distinct().OrderByDescending(d => d).ToList();
                    if (sortedDates.Any())
                    {
                        streak = 1;
                        for (int i = 0; i < sortedDates.Count - 1; i++)
                        {
                            if ((sortedDates[i] - sortedDates[i + 1]).TotalDays == 1)
                            {
                                streak++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    attendanceOverview.ActiveCampaign = new MassCampaignSummaryDto
                    {
                        CampaignId = campaign.Id,
                        Title = campaign.Title,
                        StartDate = campaign.StartDate,
                        EndDate = campaign.EndDate,
                        TargetDays = campaign.TargetDaysCount,
                        AttendedDays = attendedDays,
                        CurrentStreak = streak,
                        Status = campaign.Status,
                        RecentPresentDatesIso = attendedRecords.OrderByDescending(r => r.AttendanceDate).Take(7).Select(r => r.AttendanceDate.ToString("yyyy-MM-dd")).ToList()
                    };
                }
            }

            dto.Attendance = attendanceOverview;
        }

        private async Task PopulateAcademicHistoryAsync(
            StaffStudent360DetailDto dto,
            long studentId,
            int currentYearId,
            Enrollment? currentEnrollment)
        {
            var academicHistory = new AcademicHistoryDto();

            // Điểm số năm hiện tại
            if (currentEnrollment != null && currentEnrollment.GradeRecords.Any())
            {
                var scoreDict = new Dictionary<int, decimal?>();
                var configs = new List<GradeConfiguration>();

                foreach (var gr in currentEnrollment.GradeRecords)
                {
                    if (gr.GradeConfiguration != null)
                    {
                        configs.Add(gr.GradeConfiguration);
                        scoreDict[gr.GradeConfigurationId] = gr.Score;

                        academicHistory.CurrentGrades.Add(new CurrentGradeRecordDto
                        {
                            ColumnName = gr.GradeConfiguration.DisplayName,
                            WeightFactor = gr.GradeConfiguration.WeightFactor,
                            Score = gr.Score,
                            Semester = gr.GradeConfiguration.Semester,
                            Note = gr.Note
                        });
                    }
                }

                // Tính điểm trung bình hiện hành
                academicHistory.CurrentYearAverage = _gradeCalcService.CalculateSemesterAverage(scoreDict, configs);
                if (academicHistory.CurrentYearAverage.HasValue)
                {
                    var avg = academicHistory.CurrentYearAverage.Value;
                    academicHistory.CurrentClassification = avg >= 8.0m ? "Giỏi" : avg >= 6.5m ? "Khá" : avg >= 5.0m ? "Đạt" : "Chưa đạt";
                }
            }

            // Lịch sử 2 Niên khóa liền kề trước đó
            var allYears = await _context.AcademicYears
                .AsNoTracking()
                .OrderByDescending(y => y.StartDate)
                .ToListAsync();

            var pastYears = allYears
                .Where(y => y.Id != currentYearId)
                .Take(2)
                .ToList();

            var pastYearHistories = new List<PastYearHistoryDto>();

            foreach (var py in pastYears)
            {
                var pastEnrollment = await _context.Enrollments
                    .AsNoTracking()
                    .Include(e => e.ClassRoom)
                    .Include(e => e.GradeRecords)
                        .ThenInclude(g => g.GradeConfiguration)
                    .Include(e => e.Attendances)
                    .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassRoom != null && e.ClassRoom.AcademicYearId == py.Id);

                var historyCode = await _context.StudentCodeHistories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.StudentId == studentId && h.SourceAcademicYearId == py.Id);

                if (pastEnrollment != null || historyCode != null)
                {
                    string className = pastEnrollment?.ClassRoom?.Name ?? historyCode?.SourceClassName ?? "Chưa rõ";
                    string outcome = historyCode?.RolloverOutcome ?? (pastEnrollment?.IsPassed == true ? "PROMOTE" : "RETAIN");

                    decimal? yearAverage = null;
                    if (pastEnrollment != null && pastEnrollment.GradeRecords.Any())
                    {
                        var pScores = pastEnrollment.GradeRecords.ToDictionary(g => g.GradeConfigurationId, g => g.Score);
                        var pConfigs = pastEnrollment.GradeRecords.Where(g => g.GradeConfiguration != null).Select(g => g.GradeConfiguration!).ToList();
                        yearAverage = _gradeCalcService.CalculateSemesterAverage(pScores, pConfigs);
                    }

                    // Tỷ lệ chuyên cần năm cũ
                    double attendanceRate = 100.0;
                    if (pastEnrollment?.Attendances != null && pastEnrollment.Attendances.Any())
                    {
                        int total = pastEnrollment.Attendances.Count;
                        int attended = pastEnrollment.Attendances.Count(a => a.AttendedMass || a.ClassAttended);
                        attendanceRate = total > 0 ? Math.Round((double)attended / total * 100, 1) : 100.0;
                    }

                    var isPassed = pastEnrollment?.IsPassed ?? outcome == "PROMOTE" || outcome == "GRADUATE";
                    var classification = yearAverage.HasValue
                        ? (yearAverage.Value >= 8.0m ? "Giỏi" : yearAverage.Value >= 6.5m ? "Khá" : yearAverage.Value >= 5.0m ? "Đạt" : "Yếu")
                        : (isPassed ? "Đạt" : "Chưa đạt");

                    pastYearHistories.Add(new PastYearHistoryDto
                    {
                        AcademicYearId = py.Id,
                        AcademicYearName = py.Name,
                        ClassName = className,
                        YearAverageScore = yearAverage,
                        AttendanceRatePercent = attendanceRate,
                        RolloverOutcome = outcome,
                        RolloverOutcomeDisplay = MapOutcomeDisplay(outcome),
                        IsPassed = isPassed,
                        Classification = classification
                    });
                }
            }

            academicHistory.PastTwoYears = pastYearHistories;
            dto.Academic = academicHistory;
        }

        private async Task PopulateSacramentsAsync(StaffStudent360DetailDto dto, Student student)
        {
            var sacramentRecords = await _context.StudentSacramentRecords
                .AsNoTracking()
                .Where(r => r.StudentId == student.Id && !r.IsDeleted)
                .ToListAsync();

            var sacramentDto = new SacramentHistoryDto();

            // 1. Rửa Tội
            var baptism = sacramentRecords.FirstOrDefault(r => r.SacramentType == 1);
            if (baptism != null)
            {
                sacramentDto.Baptism = MapSacramentItem("Rửa Tội", baptism);
            }
            else if (student.BaptismDate.HasValue)
            {
                sacramentDto.Baptism = new SacramentMilestoneItemDto
                {
                    MilestoneName = "Rửa Tội",
                    SacramentType = 1,
                    IsReceived = true,
                    ReceivedDate = student.BaptismDate,
                    ParishName = "Giáo xứ Phú Trung"
                };
            }

            // 2. Rước Lễ Lần Đầu
            var firstCommunion = sacramentRecords.FirstOrDefault(r => r.SacramentType == 2);
            if (firstCommunion != null)
            {
                sacramentDto.FirstCommunion = MapSacramentItem("Rước Lễ Lần Đầu", firstCommunion);
            }

            // 3. Thêm Sức
            var confirmation = sacramentRecords.FirstOrDefault(r => r.SacramentType == 3);
            if (confirmation != null)
            {
                sacramentDto.Confirmation = MapSacramentItem("Thêm Sức", confirmation);
            }
            else if (student.ConfirmationDate.HasValue)
            {
                sacramentDto.Confirmation = new SacramentMilestoneItemDto
                {
                    MilestoneName = "Thêm Sức",
                    SacramentType = 3,
                    IsReceived = true,
                    ReceivedDate = student.ConfirmationDate,
                    ParishName = "Giáo xứ Phú Trung"
                };
            }

            // 4. Bao Đồng / Tuyên Hứa Sống Đạo
            var solemn = sacramentRecords.FirstOrDefault(r => r.SacramentType == 4);
            if (solemn != null)
            {
                sacramentDto.SolemnProfession = MapSacramentItem("Bao Đồng", solemn);
            }

            dto.Sacrament = sacramentDto;
        }

        private void PopulateParentContact(StaffStudent360DetailDto dto, Student student, string? className)
        {
            var phone = student.ParentPhone?.Trim();
            var parentDto = new ParentContactDto
            {
                ParentPhone = phone,
                FormattedPhone = FormatPhoneNumber(phone),
                Address = "Giáo xứ Phú Trung, Hạt Tân Sơn Nhì, TGP Sài Gòn"
            };

            var studentFullName = $"{student.ChristianName} {student.LastName} {student.FirstName}".Trim();
            var classDisplay = !string.IsNullOrWhiteSpace(className) ? $"lớp {className}" : "Xứ Đoàn Phú Trung";

            // Tạo các mẫu tin nhắn nhanh gửi phụ huynh qua Zalo / SMS
            parentDto.Templates = new List<ParentMessageQuickTemplateDto>
            {
                new()
                {
                    Title = "Báo vắng điểm danh Chúa Nhật",
                    Category = "ATTENDANCE",
                    Content = $"Kính gửi Quý Phụ huynh, Ban Giáo lý GX Phú Trung xin thông báo: Hôm nay em {studentFullName} ({classDisplay}) vắng Lễ/học Giáo lý. Kính mong Quý Phụ huynh lưu ý nhắc nhở em tham dự đầy đủ."
                },
                new()
                {
                    Title = "Thông báo tiến độ Chiến dịch Đi Lễ",
                    Category = "REMINDER",
                    Content = $"Kính gửi Quý Phụ huynh, em {studentFullName} ({classDisplay}) đang tham gia Chiến dịch Đi Lễ chuẩn bị Bí tích. Kính xin Quý Phụ huynh tiếp tục đồng hành và động viên em duy trì chuỗi tham dự Thánh lễ sốt sắng."
                },
                new()
                {
                    Title = "Nhắc nhở học tập & Kiểm tra Giáo lý",
                    Category = "SACRAMENT",
                    Content = $"Kính gửi Quý Phụ huynh, Ban Giáo lý xin gửi lời chào trân trọng. Sắp tới {classDisplay} sẽ có bài kiểm tra Giáo lý định kỳ. Kính mong Phụ huynh đôn đốc em {studentFullName} ôn bài thật chu đáo."
                },
                new()
                {
                    Title = "Lời khích lệ & Tuyên dương chuyên cần",
                    Category = "ENCOURAGE",
                    Content = $"Ban Giáo lý GX Phú Trung xin chân thành cảm ơn Quý Phụ huynh đã luôn đồng hành cùng em {studentFullName} ({classDisplay}). Em có tinh thần chuyên cần rất tốt trong các sinh hoạt thời gian qua."
                }
            };

            dto.ParentContact = parentDto;
        }

        public async Task RecordLookupLogAsync(long userId, long studentId, string searchTerm, string selectedOption, string? ipAddress)
        {
            try
            {
                _context.StaffQuickLookupLogs.Add(new StaffQuickLookupLog
                {
                    UserId = userId,
                    StudentId = studentId,
                    SearchTerm = searchTerm.Length > 150 ? searchTerm[..150] : searchTerm,
                    SelectedOption = string.IsNullOrWhiteSpace(selectedOption) ? "OVERVIEW" : selectedOption,
                    IpAddress = ipAddress,
                    LookedUpAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[StaffStudentLookup] Lỗi khi ghi nhận lịch sử tra cứu của User {UserId}", userId);
            }
        }

        public async Task<List<RecentLookupItemDto>> GetRecentLookupsAsync(long userId, int limit = 5)
        {
            var logs = await _context.StaffQuickLookupLogs
                .AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LookedUpAt)
                .Take(limit * 3)
                .Include(l => l.Student)
                    .ThenInclude(s => s!.Enrollments)
                        .ThenInclude(e => e.ClassRoom)
                .ToListAsync();

            var uniqueStudents = logs
                .Where(l => l.Student != null)
                .GroupBy(l => l.StudentId)
                .Select(g => g.First())
                .Take(limit)
                .ToList();

            var results = new List<RecentLookupItemDto>();
            foreach (var log in uniqueStudents)
            {
                var s = log.Student!;
                var currentClass = s.Enrollments?.OrderByDescending(e => e.EnrolledAt).FirstOrDefault()?.ClassRoom?.Name;
                results.Add(new RecentLookupItemDto
                {
                    StudentId = s.Id,
                    StudentCode = s.StudentCode,
                    ChristianName = s.ChristianName,
                    FullName = $"{s.ChristianName} {s.LastName} {s.FirstName}".Trim(),
                    ClassName = currentClass,
                    LookedUpAt = log.LookedUpAt,
                    TimeAgoDisplay = FormatTimeAgo(log.LookedUpAt)
                });
            }

            return results;
        }

        private static RecentAttendanceItemDto MapAttendanceItem(Attendance a, string sessionType)
        {
            return new RecentAttendanceItemDto
            {
                AttendanceDate = a.AttendanceDate,
                IsAttended = a.AttendedMass,
                Status = a.MassStatus,
                StatusDisplay = MapMassStatusDisplay(a.MassStatus),
                StatusBadgeColor = MapMassStatusBadge(a.MassStatus),
                CheckInTime = a.MassCheckInTime,
                SessionType = sessionType
            };
        }

        private static SacramentMilestoneItemDto MapSacramentItem(string name, StudentSacramentRecord r)
        {
            return new SacramentMilestoneItemDto
            {
                MilestoneName = name,
                SacramentType = r.SacramentType,
                IsReceived = true,
                ReceivedDate = r.ReceivedDate,
                ParishName = r.ParishName,
                PriestName = r.PriestName,
                GodparentName = r.GodparentName,
                RegistryBookNumber = r.RegistryBookNumber,
                Notes = r.Notes
            };
        }

        private static string MapMassStatusDisplay(string status) => status switch
        {
            "PRESENT" => "Có mặt đúng giờ",
            "LATE" => "Đi trễ",
            "ABSENT_PERMITTED" => "Vắng có phép",
            "ABSENT_UNPERMITTED" => "Vắng không phép",
            _ => status
        };

        private static string MapMassStatusBadge(string status) => status switch
        {
            "PRESENT" => "success",
            "LATE" => "warning text-dark",
            "ABSENT_PERMITTED" => "info text-dark",
            "ABSENT_UNPERMITTED" => "danger",
            _ => "secondary"
        };

        private static string MapClassStatusDisplay(string status) => status switch
        {
            "PRESENT" => "Có mặt học giáo lý",
            "LATE" => "Vào lớp trễ",
            "ABSENT_PERMITTED" => "Nghỉ có phép",
            "ABSENT_UNPERMITTED" => "Nghỉ không phép",
            _ => status
        };

        private static string MapClassStatusBadge(string status) => status switch
        {
            "PRESENT" => "success",
            "LATE" => "warning text-dark",
            "ABSENT_PERMITTED" => "info text-dark",
            "ABSENT_UNPERMITTED" => "danger",
            _ => "secondary"
        };

        private static string MapOutcomeDisplay(string outcome) => outcome switch
        {
            "PROMOTE" => "Lên lớp",
            "RETAIN" => "Ở lại lớp",
            "GRADUATE" => "Tốt nghiệp",
            "SKIP" => "Nghỉ học / Chuyển xứ",
            _ => outcome
        };

        private static string FormatPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "Chưa cập nhật";
            var clean = new string(phone.Where(char.IsDigit).ToArray());
            if (clean.Length == 10)
            {
                return $"{clean[..4]} {clean.Substring(4, 3)} {clean[7..]}";
            }
            return phone;
        }

        private static string FormatTimeAgo(DateTime utcDate)
        {
            var span = DateTime.UtcNow - utcDate;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
            return utcDate.AddHours(7).ToString("dd/MM/yyyy");
        }
    }
}
