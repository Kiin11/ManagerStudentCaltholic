using System.Security.Cryptography;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication ApplyDatabaseMigrations(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ParishDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ParishDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            try
            {
                logger.LogInformation("Đang kiểm tra và áp dụng Migrations cơ sở dữ liệu...");
                dbContext.Database.Migrate();
                logger.LogInformation("Áp dụng Migrations thành công!");

                if (!dbContext.Users.Any())
                {
                    logger.LogInformation("Khởi tạo tài khoản mặc định ban đầu...");
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    var adminInitPass = config["SeedSettings:AdminPassword"];
                    bool isGeneratedAdminPass = false;
                    if (string.IsNullOrWhiteSpace(adminInitPass))
                    {
                        adminInitPass = "Adm!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(9));
                        isGeneratedAdminPass = true;
                    }

                    var directorInitPass = config["SeedSettings:SpiritualDirectorPassword"];
                    bool isGeneratedDirectorPass = false;
                    if (string.IsNullOrWhiteSpace(directorInitPass))
                    {
                        directorInitPass = "Cha!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(9));
                        isGeneratedDirectorPass = true;
                    }

                    var adminUser = new User
                    {
                        Username = "admin",
                        PasswordHash = passwordHasher.HashPassword(adminInitPass),
                        FirstName = "Quản Trị Viên Kỹ Thuật",
                        LastName = "Hệ Thống",
                        ChristianName = "",
                        Role = UserRole.Admin,
                        Email = "admin@parish.local",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var spiritualDirector = new User
                    {
                        Username = "chatienuy",
                        PasswordHash = passwordHasher.HashPassword(directorInitPass),
                        FirstName = "Cha",
                        LastName = "Tuyên Úy",
                        ChristianName = "",
                        Role = UserRole.SpiritualDirector,
                        Email = "chatienuy@parish.local",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.Users.AddRange(adminUser, spiritualDirector);
                    dbContext.SaveChanges();

                    if (isGeneratedAdminPass || isGeneratedDirectorPass)
                    {
                        Console.WriteLine("==================================================================");
                        Console.WriteLine("[INIT SEED] Tài khoản khởi tạo lần đầu (Vui lòng đổi mật khẩu ngay):");
                        if (isGeneratedAdminPass)
                            Console.WriteLine($"  - Username: admin      | Password: {adminInitPass}");
                        if (isGeneratedDirectorPass)
                            Console.WriteLine($"  - Username: chatienuy  | Password: {directorInitPass}");
                        Console.WriteLine("==================================================================");
                    }

                    logger.LogInformation("Đã khởi tạo thành công 2 tài khoản hệ thống mặc định: 'admin' và 'chatienuy'.");
                }

                // FEAT-P5-07: Khởi tạo dữ liệu mẫu Kế hoạch lớp & Lịch sử phiên bản nếu chưa có
                if (!dbContext.ClassCurriculumDocuments.Any())
                {
                    logger.LogInformation("Đang khởi tạo dữ liệu mẫu Kế hoạch lớp (FEAT-P5-07)...");
                    SeedSampleCurriculumData(dbContext, logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Đã xảy ra lỗi trong quá trình tự động Migrate Database.");
                throw;
            }

            return app;
        }

        private static void SeedSampleCurriculumData(ParishDbContext dbContext, ILogger logger)
        {
            try
            {
                var year = dbContext.AcademicYears.FirstOrDefault(y => y.IsCurrent)
                           ?? dbContext.AcademicYears.FirstOrDefault();
                if (year == null)
                {
                    year = new AcademicYear
                    {
                        Name = "2026-2027",
                        StartDate = new DateTime(2026, 9, 1),
                        EndDate = new DateTime(2027, 6, 30),
                        IsCurrent = true
                    };
                    dbContext.AcademicYears.Add(year);
                    dbContext.SaveChanges();
                }

                var targetClass = dbContext.Classes.FirstOrDefault(c => c.AcademicYearId == year.Id && !c.IsDeleted)
                                  ?? dbContext.Classes.FirstOrDefault(c => !c.IsDeleted);
                if (targetClass == null)
                {
                    targetClass = new ClassRoom
                    {
                        Name = "TS 3A",
                        GradeLevel = "Thêm Sức",
                        RoomName = "Phòng B201",
                        Shift = "Sáng CN",
                        AcademicYearId = year.Id,
                        IsDeleted = false
                    };
                    dbContext.Classes.Add(targetClass);
                    dbContext.SaveChanges();
                }

                const string jsonV1 = @"[
                    {""WeekNumber"":1,""LessonDate"":""2026-09-06T00:00:00Z"",""LessonDateStr"":""06/09/2026"",""LiturgicalDay"":""CN 23 TN"",""LessonCode"":""KHAI GIẢNG"",""TopicTitle"":""Thánh lễ Khai giảng Năm học Giáo lý mới"",""KeyPoints"":""Gặp gỡ GLV, sinh hoạt nội quy"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":2,""LessonDate"":""2026-09-13T00:00:00Z"",""LessonDateStr"":""13/09/2026"",""LiturgicalDay"":""CN 24 TN"",""LessonCode"":""Bài 1"",""TopicTitle"":""Thiên Chúa Mặc Khải và Đức Tin"",""KeyPoints"":""Kinh Thánh và Truyền Thống Thánh"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":3,""LessonDate"":""2026-09-20T00:00:00Z"",""LessonDateStr"":""20/09/2026"",""LiturgicalDay"":""CN 25 TN"",""LessonCode"":""Bài 2"",""TopicTitle"":""Thiên Chúa Tạo Dựng Trời Đất"",""KeyPoints"":""Phẩm giá con người"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":4,""LessonDate"":""2026-09-27T00:00:00Z"",""LessonDateStr"":""27/09/2026"",""LiturgicalDay"":""CN 26 TN"",""LessonCode"":""LỄ HỘI"",""TopicTitle"":""Sinh Hoạt Đêm Hội Trăng Rằm - Trung Thu"",""KeyPoints"":""Vui chơi xứ đoàn"",""AssignedTeacherName"":""BĐH Xứ Đoàn"",""IsExamDay"":false,""IsDayOff"":true,""Semester"":1},
                    {""WeekNumber"":5,""LessonDate"":""2026-10-04T00:00:00Z"",""LessonDateStr"":""04/10/2026"",""LiturgicalDay"":""CN 27 TN"",""LessonCode"":""Bài 3"",""TopicTitle"":""Sự Sa Ngã và Lời Hứa Cứu Độ"",""KeyPoints"":""Tội nguyên tổ và Đấng Cứu Thế"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":6,""LessonDate"":""2026-10-11T00:00:00Z"",""LessonDateStr"":""11/10/2026"",""LiturgicalDay"":""CN 28 TN"",""LessonCode"":""Bài 4"",""TopicTitle"":""Mầu Nhiệm Thiên Chúa Ba Ngôi"",""KeyPoints"":""Chúa Cha, Con và Thánh Thần"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":7,""LessonDate"":""2026-10-18T00:00:00Z"",""LessonDateStr"":""18/10/2026"",""LiturgicalDay"":""CN 29 TN"",""LessonCode"":""Bài 5"",""TopicTitle"":""Chúa Giêsu Nhập Thể Làm Người"",""KeyPoints"":""Ngôi Hai xuống thế"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":8,""LessonDate"":""2026-10-25T00:00:00Z"",""LessonDateStr"":""25/10/2026"",""LiturgicalDay"":""CN 30 TN"",""LessonCode"":""ÔN TẬP"",""TopicTitle"":""Hệ thống hóa giáo lý từ Bài 1 đến Bài 5"",""KeyPoints"":""Chuẩn bị kiểm tra 1 tiết"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":9,""LessonDate"":""2026-11-01T00:00:00Z"",""LessonDateStr"":""01/11/2026"",""LiturgicalDay"":""Lễ Các Thánh"",""LessonCode"":""KT 1T"",""TopicTitle"":""Kiểm Tra 1 Tiết Giáo Lý HK1"",""KeyPoints"":""Nội dung 5 bài đầu"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":true,""IsDayOff"":false,""Semester"":1}
                ]";

                const string jsonV2 = @"[
                    {""WeekNumber"":1,""LessonDate"":""2026-09-06T00:00:00Z"",""LessonDateStr"":""06/09/2026"",""LiturgicalDay"":""CN 23 TN"",""LessonCode"":""KHAI GIẢNG"",""TopicTitle"":""Thánh lễ Khai giảng Năm học Giáo lý mới"",""KeyPoints"":""Gặp gỡ GLV, sinh hoạt nội quy lớp"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":2,""LessonDate"":""2026-09-13T00:00:00Z"",""LessonDateStr"":""13/09/2026"",""LiturgicalDay"":""CN 24 TN"",""LessonCode"":""Bài 1"",""TopicTitle"":""Thiên Chúa Mặc Khải và Đức Tin"",""KeyPoints"":""Kinh Thánh và Truyền Thống Thánh"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":3,""LessonDate"":""2026-09-20T00:00:00Z"",""LessonDateStr"":""20/09/2026"",""LiturgicalDay"":""CN 25 TN"",""LessonCode"":""Bài 2"",""TopicTitle"":""Thiên Chúa Tạo Dựng Trời Đất"",""KeyPoints"":""Ý nghĩa tạo dựng và phẩm giá con người"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":4,""LessonDate"":""2026-09-27T00:00:00Z"",""LessonDateStr"":""27/09/2026"",""LiturgicalDay"":""CN 26 TN"",""LessonCode"":""LỄ HỘI"",""TopicTitle"":""Sinh Hoạt Đêm Hội Trăng Rằm - Trung Thu"",""KeyPoints"":""Vui chơi cùng thiếu nhi xứ đoàn"",""AssignedTeacherName"":""BĐH Xứ Đoàn"",""IsExamDay"":false,""IsDayOff"":true,""Semester"":1},
                    {""WeekNumber"":5,""LessonDate"":""2026-10-04T00:00:00Z"",""LessonDateStr"":""04/10/2026"",""LiturgicalDay"":""CN 27 TN"",""LessonCode"":""Bài 3"",""TopicTitle"":""Sự Sa Ngã và Lời Hứa Cứu Độ"",""KeyPoints"":""Tội nguyên tổ và niềm hy vọng Đấng Cứu Thế"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":6,""LessonDate"":""2026-10-11T00:00:00Z"",""LessonDateStr"":""11/10/2026"",""LiturgicalDay"":""CN 28 TN"",""LessonCode"":""Bài 4"",""TopicTitle"":""Mầu Nhiệm Thiên Chúa Ba Ngôi"",""KeyPoints"":""Chúa Cha, Chúa Con và Chúa Thánh Thần"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":7,""LessonDate"":""2026-10-18T00:00:00Z"",""LessonDateStr"":""18/10/2026"",""LiturgicalDay"":""CN 29 TN"",""LessonCode"":""Bài 5"",""TopicTitle"":""Chúa Giêsu Nhập Thể Làm Người"",""KeyPoints"":""Ngôi Hai Thiên Chúa xuống thế làm người"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":8,""LessonDate"":""2026-10-25T00:00:00Z"",""LessonDateStr"":""25/10/2026"",""LiturgicalDay"":""CN 30 TN"",""LessonCode"":""ÔN TẬP"",""TopicTitle"":""Hệ thống hóa giáo lý từ Bài 1 đến Bài 5"",""KeyPoints"":""Giải đáp thắc mắc và chuẩn bị kiểm tra 1 tiết"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":9,""LessonDate"":""2026-11-01T00:00:00Z"",""LessonDateStr"":""01/11/2026"",""LiturgicalDay"":""Lễ Các Thánh"",""LessonCode"":""KT 1T"",""TopicTitle"":""Kiểm Tra 1 Tiết Giáo Lý HK1"",""KeyPoints"":""Nội dung 5 bài đầu và kinh nguyện"",""AssignedTeacherName"":""Anh Minh Toàn & Chị Thu Hà"",""IsExamDay"":true,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":10,""LessonDate"":""2026-11-08T00:00:00Z"",""LessonDateStr"":""08/11/2026"",""LiturgicalDay"":""CN 32 TN"",""LessonCode"":""Bài 6"",""TopicTitle"":""Cuộc Tử Nạn và Phục Sinh của Đức Kitô"",""KeyPoints"":""Mầu nhiệm Vượt Qua đem lại ơn cứu độ"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":11,""LessonDate"":""2026-11-15T00:00:00Z"",""LessonDateStr"":""15/11/2026"",""LiturgicalDay"":""CN 33 TN"",""LessonCode"":""Bài 7"",""TopicTitle"":""Chúa Thánh Thần - Đấng Ban Sự Sống"",""KeyPoints"":""Ngôi Ba Thiên Chúa và 7 ơn Chúa Thánh Thần"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":12,""LessonDate"":""2026-11-22T00:00:00Z"",""LessonDateStr"":""22/11/2026"",""LiturgicalDay"":""Lễ Chúa Kitô Vua"",""LessonCode"":""QUAN THẦY"",""TopicTitle"":""Mừng Lễ Bổn Mạng Xứ Đoàn Kitô Vua"",""KeyPoints"":""Thánh lễ trọng thể và hội trại truyền thống"",""AssignedTeacherName"":""Ban Điều Hành"",""IsExamDay"":false,""IsDayOff"":true,""Semester"":1},
                    {""WeekNumber"":13,""LessonDate"":""2026-11-29T00:00:00Z"",""LessonDateStr"":""29/11/2026"",""LiturgicalDay"":""CN 1 Mùa Vọng"",""LessonCode"":""Bài 8"",""TopicTitle"":""Hội Thánh - Dân Thiên Chúa"",""KeyPoints"":""Bốn đặc tính của Hội Thánh: Duy nhất, Thánh thiện, Công giáo, Tông truyền"",""AssignedTeacherName"":""Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":14,""LessonDate"":""2026-12-06T00:00:00Z"",""LessonDateStr"":""06/12/2026"",""LiturgicalDay"":""CN 2 Mùa Vọng"",""LessonCode"":""Bài 9"",""TopicTitle"":""Bí Tích Thêm Sức - Ấn Tín Ơn Chúa Thánh Thần"",""KeyPoints"":""Nghi thức, ý nghĩa và bổn phận người lãnh nhận"",""AssignedTeacherName"":""Anh Minh Toàn"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":15,""LessonDate"":""2026-12-13T00:00:00Z"",""LessonDateStr"":""13/12/2026"",""LiturgicalDay"":""CN 3 Mùa Vọng"",""LessonCode"":""ÔN TẬP HK1"",""TopicTitle"":""Ôn Tập Tổng Hợp Học Kỳ 1"",""KeyPoints"":""Hệ thống toàn bộ giáo lý chuẩn bị thi học kỳ"",""AssignedTeacherName"":""Anh Minh Toàn & Chị Thu Hà"",""IsExamDay"":false,""IsDayOff"":false,""Semester"":1},
                    {""WeekNumber"":16,""LessonDate"":""2026-12-20T00:00:00Z"",""LessonDateStr"":""20/12/2026"",""LiturgicalDay"":""CN 4 Mùa Vọng"",""LessonCode"":""THI HK1"",""TopicTitle"":""Thi Học Kỳ 1 Giáo Lý & Kinh Nguyện"",""KeyPoints"":""Đề thi chung toàn khối Thêm Sức"",""AssignedTeacherName"":""BĐH & Ban Giám Khảo"",""IsExamDay"":true,""IsDayOff"":false,""Semester"":1}
                ]";

                var doc = new ClassCurriculumDocument
                {
                    ClassRoomId = targetClass.Id,
                    AcademicYearId = year.Id,
                    Title = $"Kế hoạch bài giảng & Phân phối chương trình Lớp {targetClass.Name}",
                    Description = "Kế hoạch giáo lý năm học 2026-2027 đã được Cha Tuyên Úy phê duyệt",
                    CurrentVersion = "v2",
                    VersionNumber = 2,
                    ContentJson = jsonV2,
                    LastAttachmentFileName = $"KeHoach_{targetClass.Name}_v2_20261105.xlsx",
                    LastAttachmentFilePath = "/LessonPlans/DownloadCurriculumVersion/2",
                    LastAttachmentFileSize = 42560,
                    LastAttachmentContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    CreatedBy = "Anh Minh Toàn",
                    CreatedAt = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc),
                    UpdatedBy = "Anh Minh Toàn",
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                dbContext.ClassCurriculumDocuments.Add(doc);
                dbContext.SaveChanges();

                var v1 = new ClassCurriculumVersion
                {
                    ClassCurriculumDocumentId = doc.Id,
                    VersionNumber = 1,
                    VersionTag = "v1",
                    ChangeSummary = "Nạp từ file kế hoạch đầu niên khóa (Bản duyệt Cha Xứ)",
                    ContentJson = jsonV1,
                    FileName = $"KeHoach_{targetClass.Name}_v1_20260906.xlsx",
                    StoredFileName = "20260906_083000_sample_v1.xlsx",
                    FilePath = $"/LessonPlans/DownloadCurriculumVersion/{1}",
                    FileSize = 38420,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    CreatedBy = "Anh Minh Toàn",
                    CreatedAt = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc)
                };

                var v2 = new ClassCurriculumVersion
                {
                    ClassCurriculumDocumentId = doc.Id,
                    VersionNumber = 2,
                    VersionTag = "v2",
                    ChangeSummary = "Bổ sung phân công GLV phụ trách & chi tiết lịch thi HK1",
                    ContentJson = jsonV2,
                    FileName = $"KeHoach_{targetClass.Name}_v2_20261105.xlsx",
                    StoredFileName = "20261105_142000_sample_v2.xlsx",
                    FilePath = $"/LessonPlans/DownloadCurriculumVersion/{2}",
                    FileSize = 42560,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    CreatedBy = "Anh Minh Toàn",
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.ClassCurriculumVersions.AddRange(v1, v2);
                dbContext.SaveChanges();

                v1.FilePath = $"/LessonPlans/DownloadCurriculumVersion/{v1.Id}";
                v2.FilePath = $"/LessonPlans/DownloadCurriculumVersion/{v2.Id}";
                doc.LastAttachmentFilePath = v2.FilePath;

                // Đồng bộ sang bảng ClassLessonPlans
                var startDate = new DateTime(2026, 9, 6);
                for (int i = 1; i <= 16; i++)
                {
                    var lessonDate = startDate.AddDays((i - 1) * 7);
                    bool isExam = (i == 9 || i == 16);
                    bool isOff = (i == 4 || i == 12);

                    dbContext.ClassLessonPlans.Add(new ClassLessonPlan
                    {
                        ClassRoomId = targetClass.Id,
                        AcademicYearId = year.Id,
                        CurriculumDocumentId = doc.Id,
                        WeekNumber = i,
                        Semester = 1,
                        LessonDate = DateTime.SpecifyKind(lessonDate, DateTimeKind.Utc),
                        LiturgicalDay = i == 9 ? "Lễ Các Thánh" : (i == 12 ? "Lễ Chúa Kitô Vua" : $"CN {22 + i} TN"),
                        LessonCode = isExam ? (i == 9 ? "KT 1T" : "THI HK1") : (isOff ? "LỄ HỘI" : $"Bài {i}"),
                        TopicTitle = isExam ? (i == 9 ? "Kiểm Tra 1 Tiết Giáo Lý HK1" : "Thi Học Kỳ 1 Giáo Lý & Kinh Nguyện") : $"Chủ đề bài học tuần {i}",
                        KeyPoints = "Trọng tâm giáo lý bài học",
                        AssignedTeacherName = i % 2 == 0 ? "Chị Thu Hà" : "Anh Minh Toàn",
                        IsExamDay = isExam,
                        IsDayOff = isOff
                    });
                }

                dbContext.SaveChanges();
                logger.LogInformation("Khởi tạo thành công Kế hoạch lớp mẫu cho Lớp {ClassName} với 2 phiên bản (v1, v2)!", targetClass.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể khởi tạo dữ liệu mẫu Kế hoạch lớp: {Message}", ex.Message);
            }
        }
    }
}
