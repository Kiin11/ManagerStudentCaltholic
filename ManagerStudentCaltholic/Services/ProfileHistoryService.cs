using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Enum;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// Service tổng hợp lịch sử học tập (Enrollments + GradeRecords) qua LinkedStudentId/StudentId
    /// và lịch sử giảng dạy (ClassTeachers) qua UserId, nhóm theo từng niên khóa (TASK-04.1).
    /// </summary>
    public class ProfileHistoryService : IProfileHistoryService
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;

        public ProfileHistoryService(
            ParishDbContext context,
            IGradeCalculationService gradeService)
        {
            _context = context;
            _gradeService = gradeService;
        }

        public async Task<UserProfileViewModel?> BuildUserProfileAsync(long userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null) return null;

            long? linkedStudentId = user.LinkedStudentId ?? user.StudentId;
            Student? linkedStudent = null;
            if (linkedStudentId.HasValue && linkedStudentId.Value > 0)
            {
                linkedStudent = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == linkedStudentId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(user.Username))
            {
                var uname = user.Username.Trim().ToLowerInvariant();
                linkedStudent = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentCode.ToLower() == uname);
            }

            return await BuildCompositeViewModelAsync(user, linkedStudent);
        }

        public async Task<UserProfileViewModel?> BuildStudentProfileAsync(long studentId)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null) return null;

            var linkedUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.LinkedStudentId == studentId || u.StudentId == studentId);

            return await BuildCompositeViewModelAsync(linkedUser, student);
        }

        private async Task<UserProfileViewModel> BuildCompositeViewModelAsync(User? user, Student? student)
        {
            var viewModel = new UserProfileViewModel
            {
                Id = user?.Id ?? 0,
                Username = user?.Username ?? string.Empty,
                Role = user?.Role ?? "Student",
                ChristianName = user?.ChristianName ?? student?.ChristianName,
                FirstName = user?.FirstName ?? student?.FirstName ?? string.Empty,
                LastName = user?.LastName ?? student?.LastName ?? string.Empty,
                DateOfBirth = user?.DateOfBirth ?? student?.DateOfBirth,
                FeastDay = user?.FeastDay ?? student?.FeastDay,
                BaptismDate = user?.BaptismDate ?? student?.BaptismDate,
                FirstCommunionDate = user?.FirstCommunionDate ?? student?.FirstCommunionDate,
                ConfirmationDate = user?.ConfirmationDate ?? student?.ConfirmationDate,
                BaoDongPromiseDate = user?.BaoDongPromiseDate ?? student?.BaoDongPromiseDate,
                CatechistPromiseDate = user?.CatechistPromiseDate ?? student?.CatechistPromiseDate,
                Email = user?.Email,
                PhoneNumber = user?.PhoneNumber ?? student?.ParentPhone,
                Address = user?.Address,
                AvatarCloudFileId = user?.AvatarCloudFileId,
                StudentAvatarCloudFileId = student?.AvatarCloudFileId,
                AvatarUrl = user?.AvatarCloudFileId.HasValue == true
                    ? $"/CloudMedia/File/{user.AvatarCloudFileId.Value}"
                    : (!string.IsNullOrWhiteSpace(user?.AvatarUrl)
                        ? user!.AvatarUrl
                        : (student?.AvatarCloudFileId.HasValue == true
                            ? $"/CloudMedia/File/{student.AvatarCloudFileId.Value}"
                            : null)),
                LastLoginAt = user?.LastLoginAt,
                ManagedGradeLevel = user?.ManagedGradeLevel,
                LinkedStudentId = student?.Id,
                LinkedStudentCode = student?.StudentCode,
                StudentDateOfBirth = student?.DateOfBirth,
                StudentGender = student?.Gender,
                ParentPhone = student?.ParentPhone,
                IsStudentActive = student?.IsActive ?? true
            };

            // 1. Lấy lịch sử giảng dạy qua UserId (nếu có tài khoản)
            var teachingAssignments = new List<AssignedClassItemDto>();
            var yearLookup = new Dictionary<int, (string Name, DateTime StartDate, bool IsCurrent)>();

            if (user != null)
            {
                // SEC-05 FIX: Chỉ lấy phân công giảng dạy khớp chính xác với khóa ngoại UserId
                var rawTeaching = await _context.ClassTeachers
                    .Include(ct => ct.ClassRoom)
                        .ThenInclude(c => c.AcademicYear)
                    .Include(ct => ct.ClassRoom)
                        .ThenInclude(c => c.Enrollments)
                    .AsSplitQuery()
                    .Where(ct => ct.UserId == user.Id)
                    .OrderByDescending(ct => ct.ClassRoom.AcademicYear.StartDate)
                    .ThenBy(ct => ct.ClassRoom.Name)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var ct in rawTeaching)
                {
                    if (ct.ClassRoom?.AcademicYear == null) continue;

                    bool isAfternoon = GradeLevel.IsAfternoonSession(ct.ClassRoom.Session, ct.ClassRoom.GradeLevel);
                    var year = ct.ClassRoom.AcademicYear;
                    yearLookup[year.Id] = (year.Name, year.StartDate, year.IsCurrent);

                    teachingAssignments.Add(new AssignedClassItemDto
                    {
                        ClassId = ct.ClassRoomId,
                        ClassName = ct.ClassRoom.Name,
                        GradeLevel = ct.ClassRoom.GradeLevel,
                        Session = isAfternoon ? GradeLevel.SessionAfternoon : GradeLevel.SessionMorning,
                        SessionDisplay = isAfternoon ? "Ca Chiều" : "Ca Sáng",
                        AcademicYearId = year.Id,
                        AcademicYearName = year.Name,
                        IsCurrentYear = year.IsCurrent,
                        RoleInClass = ct.RoleInClass,
                        RoleName = ct.RoleInClass == "HEAD" ? "Chủ nhiệm" :
                                   ct.RoleInClass == "MEMBER" ? "Giáo Lý Viên" : "Dự bị / Trợ tá",
                        RoomNumber = ct.ClassRoom.RoomName,
                        TotalStudents = ct.ClassRoom.Enrollments.Count,
                        AssignedAt = ct.AssignedAt
                    });
                }

                viewModel.CurrentClasses = teachingAssignments.Where(c => c.IsCurrentYear).ToList();
                viewModel.TeachingHistory = teachingAssignments
                    .Where(c => !c.IsCurrentYear)
                    .GroupBy(c => new { c.AcademicYearId, c.AcademicYearName })
                    .Select(g => new TeachingHistoryByYearDto
                    {
                        AcademicYearId = g.Key.AcademicYearId,
                        AcademicYearName = g.Key.AcademicYearName,
                        Classes = g.ToList()
                    })
                    .ToList();
            }

            // 2. Lấy lịch sử học tập (Enrollments + GradeRecords) qua LinkedStudentId/Student.Id
            var learningItems = new List<StudentLearningHistoryItemDto>();
            var sacDict = new Dictionary<int, DateTime>();
            if (student != null)
            {
                var enrollments = await _context.Enrollments
                    .Include(e => e.ClassRoom)
                        .ThenInclude(c => c.AcademicYear)
                    .Include(e => e.GradeRecords)
                    .AsSplitQuery()
                    .Where(e => e.StudentId == student.Id)
                    .OrderByDescending(e => e.ClassRoom.AcademicYear.StartDate)
                    .ThenBy(e => e.ClassRoom.Name)
                    .AsNoTracking()
                    .ToListAsync();

                var academicYearIds = enrollments.Select(e => e.ClassRoom.AcademicYearId).Distinct().ToList();
                var enrollmentIds = enrollments.Select(e => e.Id).ToList();

                var gradeConfigsByYear = academicYearIds.Any()
                    ? await _context.GradeConfigurations
                        .AsNoTracking()
                        .Where(g => academicYearIds.Contains(g.AcademicYearId))
                        .OrderBy(g => g.Semester)
                        .ThenBy(g => g.ColumnIndex)
                        .ToListAsync()
                    : new List<GradeConfiguration>();

                var attendances = enrollmentIds.Any()
                    ? await _context.Attendances
                        .AsNoTracking()
                        .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                        .ToListAsync()
                    : new List<Attendance>();

                foreach (var e in enrollments)
                {
                    if (e.ClassRoom?.AcademicYear == null) continue;

                    var cls = e.ClassRoom;
                    var year = cls.AcademicYear;
                    yearLookup[year.Id] = (year.Name, year.StartDate, year.IsCurrent);

                    var yearConfigs = gradeConfigsByYear
                        .Where(g => g.AcademicYearId == year.Id &&
                                    (g.GradeLevel == "ALL" || g.GradeLevel == cls.GradeLevel) &&
                                    (g.ClassRoomId == null || g.ClassRoomId == cls.Id))
                        .ToList();

                    var hk1Configs = yearConfigs.Where(g => g.Semester == 1).ToList();
                    var hk2Configs = yearConfigs.Where(g => g.Semester == 2).ToList();

                    var hk1ConfigIds = hk1Configs.Select(c => c.Id).ToHashSet();
                    var hk2ConfigIds = hk2Configs.Select(c => c.Id).ToHashSet();

                    var hk1Scores = e.GradeRecords
                        .Where(r => hk1ConfigIds.Contains(r.GradeConfigurationId))
                        .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                    var hk2Scores = e.GradeRecords
                        .Where(r => hk2ConfigIds.Contains(r.GradeConfigurationId))
                        .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                    var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Scores, hk1Configs);
                    var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Scores, hk2Configs);
                    var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

                    var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                    double attRate = 100.0;
                    if (attList.Count > 0)
                    {
                        var presentCount = attList.Count(a => a.ClassAttended && (a.ClassStatus == AttendanceStatus.Present || a.ClassStatus == AttendanceStatus.Late));
                        attRate = Math.Round((presentCount / (double)attList.Count) * 100, 1);
                    }

                    var effectiveAvg = yearAvg ?? sem2Avg ?? sem1Avg;
                    string rank = effectiveAvg.HasValue
                        ? _gradeService.EvaluateStudent(effectiveAvg, attRate, cls.GradeLevel, 0).AcademicRank
                        : "Chưa có điểm";

                    bool isAfternoon = GradeLevel.IsAfternoonSession(cls.Session, cls.GradeLevel);
                    learningItems.Add(new StudentLearningHistoryItemDto
                    {
                        EnrollmentId = e.Id,
                        ClassId = cls.Id,
                        ClassName = cls.Name,
                        GradeLevel = cls.GradeLevel,
                        Session = isAfternoon ? GradeLevel.SessionAfternoon : GradeLevel.SessionMorning,
                        SessionDisplay = isAfternoon ? "Ca Chiều" : "Ca Sáng",
                        AcademicYearId = year.Id,
                        AcademicYearName = year.Name,
                        IsCurrentYear = year.IsCurrent,
                        Semester1Average = sem1Avg,
                        Semester2Average = sem2Avg,
                        YearAverage = yearAvg,
                        AcademicRank = rank,
                        IsPassed = e.IsPassed
                    });
                }

                // 3. Đọc thêm Sổ bộ Bí tích & Mã học sinh cũ của Student liên kết (nếu có)
                var sacRecords = await _context.StudentSacramentRecords
                    .AsNoTracking()
                    .Where(r => r.StudentId == student.Id)
                    .ToListAsync();
                sacDict = sacRecords
                    .GroupBy(r => r.SacramentType)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReceivedDate).First().ReceivedDate);

                var codeHistories = await _context.StudentCodeHistories
                    .AsNoTracking()
                    .Where(h => h.StudentId == student.Id)
                    .OrderBy(h => h.ChangedAt)
                    .ToListAsync();

                viewModel.LegacyStudentCodes = codeHistories
                    .SelectMany(h => new[] { h.OriginalCode, h.ArchivedYearCode })
                    .Where(c => !string.IsNullOrWhiteSpace(c) &&
                                !string.Equals(c, student.StudentCode, StringComparison.OrdinalIgnoreCase) &&
                                (user == null || !string.Equals(c, user.Username, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            // 3B. Xây dựng 5 Cột mốc Đức tin & Tuyên hứa cho cả User & Student liên kết (TASK-04.3)
            if (user != null || student != null)
            {
                DateTime? moc1 = user?.BaptismDate ?? student?.BaptismDate ?? (sacDict.TryGetValue((int)SacramentTypeEnum.Baptism, out var d1) ? d1 : null);
                DateTime? moc2 = user?.FirstCommunionDate ?? student?.FirstCommunionDate ?? (sacDict.TryGetValue((int)SacramentTypeEnum.FirstCommunion, out var d2) ? d2 : null);
                DateTime? moc3 = user?.ConfirmationDate ?? student?.ConfirmationDate ?? (sacDict.TryGetValue((int)SacramentTypeEnum.Confirmation, out var d3) ? d3 : null);
                DateTime? moc4 = user?.BaoDongPromiseDate ?? student?.BaoDongPromiseDate ?? (sacDict.TryGetValue((int)SacramentTypeEnum.ProfessionOfFaith, out var d4) ? d4 : null);
                DateTime? moc5 = user?.CatechistPromiseDate ?? student?.CatechistPromiseDate;

                viewModel.BaptismDate = moc1;
                viewModel.FirstCommunionDate = moc2;
                viewModel.ConfirmationDate = moc3;
                viewModel.BaoDongPromiseDate = moc4;
                viewModel.CatechistPromiseDate = moc5;

                DateTime? effectiveDob = user?.DateOfBirth ?? student?.DateOfBirth;
                bool hasValidDob = effectiveDob.HasValue;
                int currentAge = hasValidDob ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, VietnamTimeHelper.Today) : 0;
                int? ageM5 = (hasValidDob && moc5.HasValue) ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, moc5.Value) : null;
                bool isUnder18AtPromise = hasValidDob && moc5.HasValue && !AgeValidationHelper.IsAtLeastAge(effectiveDob!.Value, moc5.Value, 18);
                bool isCurrentlyProbationaryAndUnder18 = hasValidDob && !moc5.HasValue && currentAge < 18 &&
                    (user?.Role == UserRole.ProbationaryTeacher || learningItems.Any(l => GradeLevel.IsProbationaryGrade(l.GradeLevel)));

                viewModel.IsUnder18CatechistWarning = isUnder18AtPromise || isCurrentlyProbationaryAndUnder18;
                if (isUnder18AtPromise)
                {
                    viewModel.CatechistWarningMessage = $"Cảnh báo: Ngày Tuyên hứa GLV ({moc5:dd/MM/yyyy}) ghi nhận khi mới {ageM5} tuổi (quy định tối thiểu 18 tuổi).";
                }
                else if (isCurrentlyProbationaryAndUnder18)
                {
                    viewModel.CatechistWarningMessage = $"Hiện tại {currentAge} tuổi — cần đủ 18 tuổi trở lên mới đủ điều kiện Hoàn thành Dự Bị & Tuyên hứa GLV.";
                }

                viewModel.FaithMilestones = new List<FaithMilestoneViewModel>
                {
                    new()
                    {
                        StepNumber = 1,
                        MilestoneName = "Bí tích Rửa Tội",
                        FieldName = nameof(Student.BaptismDate),
                        EventDate = moc1,
                        AgeAtMilestone = (hasValidDob && moc1.HasValue) ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, moc1.Value) : null,
                        IsCompleted = moc1.HasValue,
                        Description = "Khởi đầu đời sống Kitô hữu, định danh hồ sơ Thiếu nhi."
                    },
                    new()
                    {
                        StepNumber = 2,
                        MilestoneName = "Xưng Tội & Rước Lễ Lần Đầu",
                        FieldName = nameof(Student.FirstCommunionDate),
                        EventDate = moc2,
                        AgeAtMilestone = (hasValidDob && moc2.HasValue) ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, moc2.Value) : null,
                        IsCompleted = moc2.HasValue,
                        Description = "Hoàn tất Khối Rước Lễ (Rước Lễ 2), đủ điều kiện lên Khối Thêm Sức."
                    },
                    new()
                    {
                        StepNumber = 3,
                        MilestoneName = "Bí tích Thêm Sức",
                        FieldName = nameof(Student.ConfirmationDate),
                        EventDate = moc3,
                        AgeAtMilestone = (hasValidDob && moc3.HasValue) ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, moc3.Value) : null,
                        IsCompleted = moc3.HasValue,
                        Description = "Hoàn tất Khối Thêm Sức, điều kiện tiên quyết chuyển lên Khối Bao Đồng (Ca Chiều)."
                    },
                    new()
                    {
                        StepNumber = 4,
                        MilestoneName = "Tuyên Hứa Dự Bị (Bao Đồng)",
                        FieldName = nameof(Student.BaoDongPromiseDate),
                        EventDate = moc4,
                        AgeAtMilestone = (hasValidDob && moc4.HasValue) ? AgeValidationHelper.CalculateAgeAtDate(effectiveDob!.Value, moc4.Value) : null,
                        IsCompleted = moc4.HasValue,
                        Description = "Hoàn tất Khối Bao Đồng / Tuyên hứa bước vào giai đoạn Dự Bị GLV."
                    },
                    new()
                    {
                        StepNumber = 5,
                        MilestoneName = "Tuyên Hứa Giáo Lý Viên",
                        FieldName = nameof(Student.CatechistPromiseDate),
                        EventDate = moc5,
                        AgeAtMilestone = ageM5,
                        IsCompleted = moc5.HasValue && !isUnder18AtPromise,
                        IsWarning = viewModel.IsUnder18CatechistWarning,
                        WarningMessage = viewModel.CatechistWarningMessage,
                        Description = "Hoàn tất Dự Bị và đủ 18 tuổi để tuyên hứa Giáo lý viên chính thức."
                    }
                };
            }

            // 4. Hợp nhất Timeline song song Vế 1 (Lớp Học) & Vế 2 (Lớp Dạy) theo từng Niên khóa
            var allYearIds = yearLookup.Keys.ToList();
            viewModel.DualTimeline = allYearIds
                .Select(yId =>
                {
                    var yInfo = yearLookup[yId];
                    return new ProfileDualYearTimelineDto
                    {
                        AcademicYearId = yId,
                        AcademicYearName = yInfo.Name,
                        StartDate = yInfo.StartDate,
                        IsCurrentYear = yInfo.IsCurrent,
                        LearningClasses = learningItems.Where(l => l.AcademicYearId == yId).ToList(),
                        TeachingClasses = teachingAssignments.Where(t => t.AcademicYearId == yId).ToList()
                    };
                })
                .OrderByDescending(y => y.StartDate)
                .ToList();

            return viewModel;
        }
    }
}
