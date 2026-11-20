using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class EnrollmentsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<EnrollmentsController> _logger;

        public EnrollmentsController(ParishDbContext context, ILogger<EnrollmentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Enrollments/Manage?classId=... (Màn hình điều phối xếp lớp)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Manage(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học yêu cầu.";
                return RedirectToAction("Index", "Classes");
            }

            if (targetClass.AcademicYear is null)
            {
                TempData["ErrorMessage"] = "Niên khóa của lớp học không tồn tại.";
                return RedirectToAction("Index", "Classes");
            }

            var yearId = targetClass.AcademicYearId;
            bool isProbationaryClass = GradeLevel.IsProbationaryGrade(targetClass.GradeLevel);

            if (isProbationaryClass)
            {
                await EnsureProbationaryAccountsSyncedAsync();
            }

            // 1. Lấy danh sách học sinh ĐÃ Ở TRONG LỚP NÀY
            var enrolledList = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == classId)
                .OrderBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .Select(e => new EnrolledStudentItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    FullName = (e.Student.ChristianName + " " + e.Student.FirstName + " " + e.Student.LastName).Trim(),
                    Gender = e.Student.Gender,
                    DateOfBirth = e.Student.DateOfBirth,
                    EnrolledAt = e.EnrolledAt
                })
                .ToListAsync();

            // 2. Tìm các học sinh ĐÃ ĐƯỢC XẾP LỚP trong niên khóa này (bất kể lớp nào)
            // Nhằm đảm bảo quy tắc 1 em chỉ học đúng 1 lớp trong 1 niên khóa / 1 lớp Dự Bị cùng lúc
            var studentIdsAlreadyEnrolledInYear = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.ClassRoom)
                .Where(e => e.ClassRoom.AcademicYearId == yearId || e.AcademicYearId == yearId)
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            List<UnassignedStudentItem> unassignedList;

            if (isProbationaryClass)
            {
                // Khối Dự Bị: Chỉ hiển thị các tài khoản vai trò Dự Bị chưa được xếp vào bất kỳ lớp nào,
                // sử dụng trực tiếp Username làm Mã học viên
                unassignedList = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.LinkedStudent)
                    .Where(u => u.IsActive &&
                                u.Role == UserRole.ProbationaryTeacher &&
                                u.LinkedStudentId.HasValue &&
                                u.LinkedStudent != null &&
                                u.LinkedStudent.IsActive &&
                                !studentIdsAlreadyEnrolledInYear.Contains(u.LinkedStudentId.Value))
                    .OrderBy(u => u.Username)
                    .Select(u => new UnassignedStudentItem
                    {
                        StudentId = u.LinkedStudentId!.Value,
                        StudentCode = u.Username,
                        FullName = (u.ChristianName + " " + u.FirstName + " " + u.LastName).Trim(),
                        Gender = u.LinkedStudent!.Gender,
                        DateOfBirth = u.LinkedStudent.DateOfBirth,
                        ParentPhone = u.PhoneNumber ?? u.LinkedStudent.ParentPhone
                    })
                    .ToListAsync();
            }
            else
            {
                // Các khối thiếu nhi thường: Loại trừ học viên thuộc tài khoản Dự Bị để tránh xếp nhầm khối
                var probationaryStudentIds = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Role == UserRole.ProbationaryTeacher && (u.LinkedStudentId.HasValue || u.StudentId.HasValue))
                    .Select(u => u.LinkedStudentId ?? u.StudentId!.Value)
                    .Distinct()
                    .ToListAsync();

                // 3. Lấy danh sách học sinh CHƯA CÓ LỚP trong niên khóa này và đang Active
                unassignedList = await _context.Students
                    .AsNoTracking()
                    .Where(s => s.IsActive &&
                                !studentIdsAlreadyEnrolledInYear.Contains(s.Id) &&
                                !probationaryStudentIds.Contains(s.Id))
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .Select(s => new UnassignedStudentItem
                    {
                        StudentId = s.Id,
                        StudentCode = s.StudentCode,
                        FullName = (s.ChristianName + " " + s.FirstName + " " + s.LastName).Trim(),
                        Gender = s.Gender,
                        DateOfBirth = s.DateOfBirth,
                        ParentPhone = s.ParentPhone
                    })
                    .ToListAsync();
            }

            var viewModel = new ClassEnrollmentViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearId = targetClass.AcademicYearId,
                AcademicYearName = targetClass.AcademicYear.Name,
                EnrolledStudents = enrolledList,
                UnassignedStudents = unassignedList
            };

            return View(viewModel);
        }

        /// <summary>
        /// 2. POST: /Enrollments/AssignStudents (Xếp học sinh vào lớp - Ajax)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignStudents([FromBody] AssignStudentsRequest request)
        {
            if (request == null || !request.StudentIds.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 học sinh để xếp lớp." });
            }

            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .FirstOrDefaultAsync(c => c.Id == request.ClassId);

            if (targetClass == null)
            {
                return Json(new { success = false, message = "Lớp học không tồn tại." });
            }

            if (targetClass.AcademicYear is null)
            {
                return Json(new { success = false, message = "Niên khóa của lớp học không tồn tại." });
            }

            var yearId = targetClass.AcademicYearId;
            bool isProbationaryClass = GradeLevel.IsProbationaryGrade(targetClass.GradeLevel);

            // KIỂM TRA CHẶN TRÙNG LẶP (TASK-306):
            // Lấy danh sách học sinh đã có lớp trong niên khóa này
            var enrolledInYear = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => (e.ClassRoom.AcademicYearId == yearId || e.AcademicYearId == yearId) &&
                            request.StudentIds.Contains(e.StudentId))
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            // Lọc ra các em thực sự chưa có lớp
            var validStudentIds = request.StudentIds.Except(enrolledInYear).Distinct().ToList();

            if (!validStudentIds.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Tất cả các học viên được chọn đều đã được xếp vào một lớp khác trong niên khóa này!"
                });
            }

            List<User> validProbationaryUsers = new();
            if (isProbationaryClass)
            {
                validProbationaryUsers = await _context.Users
                    .Include(u => u.LinkedStudent)
                    .Where(u => u.IsActive &&
                                u.Role == UserRole.ProbationaryTeacher &&
                                u.LinkedStudentId.HasValue &&
                                u.LinkedStudent != null &&
                                u.LinkedStudent.IsActive &&
                                validStudentIds.Contains(u.LinkedStudentId.Value))
                    .ToListAsync();

                validStudentIds = validProbationaryUsers
                    .Select(u => u.LinkedStudentId!.Value)
                    .Distinct()
                    .ToList();

                if (!validStudentIds.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Khối Dự Bị chỉ tiếp nhận tài khoản vai trò 'Dự Bị' chưa được xếp vào lớp nào!"
                    });
                }
            }
            else
            {
                var probationaryStudentIds = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Role == UserRole.ProbationaryTeacher && (u.LinkedStudentId.HasValue || u.StudentId.HasValue))
                    .Select(u => u.LinkedStudentId ?? u.StudentId!.Value)
                    .Distinct()
                    .ToListAsync();

                validStudentIds = validStudentIds.Except(probationaryStudentIds).ToList();
                if (!validStudentIds.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Tài khoản vai trò Dự Bị chỉ được phép xếp vào các lớp Khối Dự Bị (Dự Bị 1..4)!"
                    });
                }
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    if (isProbationaryClass)
                    {
                        foreach (var pu in validProbationaryUsers)
                        {
                            if (pu.LinkedStudent != null && pu.LinkedStudent.StudentCode != pu.Username)
                            {
                                pu.LinkedStudent.StudentCode = pu.Username;
                            }
                        }
                    }

                    var newEnrollments = validStudentIds.Select(sid => new Enrollment
                    {
                        StudentId = sid,
                        ClassRoomId = targetClass.Id,
                        AcademicYearId = targetClass.AcademicYearId, // Bổ sung gán AcademicYearId
                        EnrolledAt = DateTime.UtcNow
                    }).ToList();

                    await _context.Enrollments.AddRangeAsync(newEnrollments);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Đã xếp {Count} học sinh vào lớp {Class} (Niên khóa: {Year})",
                    validStudentIds.Count, targetClass.Name, targetClass.AcademicYear.Name);

                var message = $"Đã xếp thành công {validStudentIds.Count} học sinh vào lớp {targetClass.Name}.";
                if (enrolledInYear.Any())
                {
                    message += $" (Bỏ qua {enrolledInYear.Count} em đã có lớp trước đó).";
                }

                return Json(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xếp học sinh vào lớp ID: {ClassId}", request.ClassId);
                return Json(new { success = false, message = "Có lỗi xảy ra trong quá trình xếp lớp. Vui lòng thử lại." });
            }
        }

        /// <summary>
        /// 3. POST: /Enrollments/RemoveStudent (Rút học sinh khỏi lớp)
        /// </summary>
        /// <param name="enrollmentId"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(long enrollmentId)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Attendances)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin xếp lớp." });
            }

            if (enrollment.Attendances.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể rút học sinh này vì đã có dữ liệu điểm danh trong lớp! Vui lòng kiểm tra lại."
                });
            }

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã đưa học sinh ra khỏi lớp thành công!" });
        }

        /// <summary>
        /// Tự động đồng bộ 1-1 tài khoản vai trò Dự Bị (ProbationaryTeacher) với hồ sơ Student (StudentCode = Username)
        /// </summary>
        private async Task EnsureProbationaryAccountsSyncedAsync()
        {
            var probUsers = await _context.Users
                .Where(u => u.Role == UserRole.ProbationaryTeacher)
                .ToListAsync();

            if (!probUsers.Any()) return;

            var usedLinkedStudentIds = (await _context.Users
                .AsNoTracking()
                .Where(u => u.LinkedStudentId.HasValue)
                .Select(u => u.LinkedStudentId!.Value)
                .ToListAsync())
                .ToHashSet();

            var existingLinkedIds = probUsers
                .Select(u => u.LinkedStudentId ?? u.StudentId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var existingStudentsById = existingLinkedIds.Any()
                ? await _context.Students
                    .Where(s => existingLinkedIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id)
                : new Dictionary<long, Student>();

            var usernames = probUsers.Select(u => u.Username.ToLower()).Distinct().ToList();
            var studentsMatchingUsernames = await _context.Students
                .Where(s => usernames.Contains(s.StudentCode.ToLower()))
                .ToListAsync();

            var pendingPairs = new List<(User User, Student Student)>();
            bool hasChanges = false;

            foreach (var u in probUsers)
            {
                long? linkedId = u.LinkedStudentId ?? u.StudentId;
                if (linkedId.HasValue && existingStudentsById.TryGetValue(linkedId.Value, out var existingStudent))
                {
                    if (!u.LinkedStudentId.HasValue && !usedLinkedStudentIds.Contains(existingStudent.Id))
                    {
                        u.LinkedStudentId = existingStudent.Id;
                        usedLinkedStudentIds.Add(existingStudent.Id);
                        hasChanges = true;
                    }

                    if (!string.Equals(existingStudent.StudentCode, u.Username, StringComparison.Ordinal))
                    {
                        existingStudent.StudentCode = u.Username;
                        hasChanges = true;
                    }

                    continue;
                }

                var matchByCode = studentsMatchingUsernames.FirstOrDefault(s =>
                    string.Equals(s.StudentCode, u.Username, StringComparison.OrdinalIgnoreCase) &&
                    !usedLinkedStudentIds.Contains(s.Id));

                if (matchByCode != null)
                {
                    matchByCode.StudentCode = u.Username;
                    u.LinkedStudentId = matchByCode.Id;
                    u.StudentId = matchByCode.Id;
                    usedLinkedStudentIds.Add(matchByCode.Id);
                    hasChanges = true;
                    continue;
                }

                var rawFirst = string.IsNullOrWhiteSpace(u.FirstName) ? u.Username : u.FirstName.Trim();
                var rawLast = string.IsNullOrWhiteSpace(u.LastName) ? u.Username : u.LastName.Trim();
                var rawChristian = string.IsNullOrWhiteSpace(u.ChristianName) ? "Dự Bị" : u.ChristianName.Trim();

                var newStudent = new Student
                {
                    StudentCode = u.Username,
                    ChristianName = rawChristian.Length > 50 ? rawChristian[..50] : rawChristian,
                    FirstName = rawFirst.Length > 90 ? rawFirst[..90] : rawFirst,
                    LastName = rawLast.Length > 10 ? rawLast[..10] : rawLast,
                    Gender = InferGenderFromName(u.ChristianName, u.FirstName),
                    DateOfBirth = u.DateOfBirth.HasValue
                        ? DateTime.SpecifyKind(u.DateOfBirth.Value.Date, DateTimeKind.Utc)
                        : new DateTime(DateTime.UtcNow.Year - 16, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ParentPhone = string.IsNullOrWhiteSpace(u.PhoneNumber)
                        ? null
                        : (u.PhoneNumber.Trim().Length > 15 ? u.PhoneNumber.Trim()[..15] : u.PhoneNumber.Trim()),
                    IsActive = u.IsActive
                };

                pendingPairs.Add((u, newStudent));
                hasChanges = true;
            }

            if (!hasChanges) return;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (pendingPairs.Any())
                {
                    await _context.Students.AddRangeAsync(pendingPairs.Select(p => p.Student));
                    await _context.SaveChangesAsync();

                    foreach (var pair in pendingPairs)
                    {
                        pair.User.LinkedStudentId = pair.Student.Id;
                        pair.User.StudentId = pair.Student.Id;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }

        private static string InferGenderFromName(string? christianName, string? firstName)
        {
            string combined = $" {christianName ?? ""} {firstName ?? ""} ".ToLowerInvariant();
            string[] femaleMarkers =
            {
                " maria ", " têrêsa ", " teresa ", " anna ", " lucia ", " lusia ",
                " agatha ", " agnes ", " marta ", " catarina ", " rosa ", " mônica ",
                " monica ", " elisabeth ", " madalena ", " cêcilia ", " clara ", " thị "
            };

            return femaleMarkers.Any(m => combined.Contains(m)) ? "Nữ" : "Nam";
        }
    }
}
