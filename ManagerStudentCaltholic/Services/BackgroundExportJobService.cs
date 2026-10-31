using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class BackgroundExportJobService : IBackgroundExportJobService
    {
        private readonly ParishDbContext _context;
        private readonly IStudentExcelService _studentExcelService;
        private readonly ICacheService _cache;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BackgroundExportJobService> _logger;

        public BackgroundExportJobService(
            ParishDbContext context,
            IStudentExcelService studentExcelService,
            ICacheService cache,
            IWebHostEnvironment env,
            ILogger<BackgroundExportJobService> logger)
        {
            _context = context;
            _studentExcelService = studentExcelService;
            _cache = cache;
            _env = env;
            _logger = logger;
        }

        public static string GetJobCacheKey(string jobTrackerId) => $"bg_export_job:{jobTrackerId}";

        /// <summary>
        /// 1. Tác vụ nền xuất danh sách học sinh theo Lớp (TASK-P3-10.2)
        /// </summary>
        public async Task GenerateClassExcelReportJobAsync(string jobTrackerId, int classId, string requestedBy)
        {
            var cacheKey = GetJobCacheKey(jobTrackerId);
            try
            {
                var targetClass = await _context.Classes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (targetClass == null)
                {
                    await MarkJobFailedAsync(cacheKey, jobTrackerId, "Không tìm thấy lớp học yêu cầu.");
                    return;
                }

                var bytes = await _studentExcelService.ExportClassListToExcelAsync(classId);
                var safeClassName = SanitizeFileNamePart(targetClass.Name);
                var fileName = $"DanhSach_Lop_{safeClassName}_{DateTime.Now:yyyyMMdd_HHmmss}_{jobTrackerId[..6]}.xlsx";

                var exportsDir = Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports");
                Directory.CreateDirectory(exportsDir);

                var fullPath = Path.Combine(exportsDir, fileName);
                await File.WriteAllBytesAsync(fullPath, bytes);

                await _cache.SetAsync(cacheKey, new BackgroundExportMetaDto
                {
                    JobId = jobTrackerId,
                    Status = "COMPLETED",
                    Title = $"Danh sách lớp {targetClass.Name}",
                    FileName = fileName,
                    RequestedBy = requestedBy,
                    CompletedAtUtc = DateTime.UtcNow
                }, TimeSpan.FromHours(24));

                _logger.LogInformation("[Hangfire] Hoàn tất xuất Excel ngầm cho lớp {ClassName} (File: {FileName})", targetClass.Name, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Hangfire] Lỗi khi xuất Excel ngầm cho lớp ID {ClassId}", classId);
                await MarkJobFailedAsync(cacheKey, jobTrackerId, "Đã xảy ra lỗi khi tạo tệp báo cáo.");
                throw;
            }
        }

        /// <summary>
        /// 2. Tác vụ nền xuất Báo cáo Tổng hợp Toàn Giáo Xứ / Toàn Khối nhiều trang tính (TASK-P3-10.2)
        /// </summary>
        public async Task GenerateParishFullExcelReportJobAsync(string jobTrackerId, int academicYearId, string? gradeLevel, string requestedBy)
        {
            var cacheKey = GetJobCacheKey(jobTrackerId);
            try
            {
                var year = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == academicYearId)
                           ?? await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);

                if (year == null)
                {
                    await MarkJobFailedAsync(cacheKey, jobTrackerId, "Không tìm thấy niên khóa để xuất báo cáo.");
                    return;
                }

                var classesQuery = _context.Classes
                    .Include(c => c.Enrollments)
                        .ThenInclude(e => e.Student)
                    .Where(c => c.AcademicYearId == year.Id);

                if (!string.IsNullOrWhiteSpace(gradeLevel) && gradeLevel != "ALL")
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == gradeLevel);
                }

                var classes = await classesQuery
                    .OrderBy(c => c.GradeLevel)
                    .ThenBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();

                var allEnrollmentIds = classes.SelectMany(c => c.Enrollments.Select(e => e.Id)).ToList();
                var allAttendances = await _context.Attendances
                    .Where(a => allEnrollmentIds.Contains(a.EnrollmentId))
                    .AsNoTracking()
                    .ToListAsync();

                using var workbook = new XLWorkbook();

                // Sheet 1: Tổng hợp sĩ số & chuyên cần các lớp
                var summarySheet = workbook.Worksheets.Add("TongHop_ToanXu");
                summarySheet.Cell("A1").Value = $"BÁO CÁO TỔNG HỢP GIÁO LÝ TOÀN GIÁO XỨ - NIÊN KHÓA {year.Name}";
                summarySheet.Cell("A1").Style.Font.Bold = true;
                summarySheet.Cell("A1").Style.Font.FontSize = 14;
                summarySheet.Range("A1:F1").Merge();

                string[] sumHeaders = { "STT", "Khối Ngành", "Tên Lớp", "Phòng Học", "Sĩ Số Đang Học", "Tổng Lượt Điểm Danh Lễ CN" };
                for (int i = 0; i < sumHeaders.Length; i++)
                {
                    var cell = summarySheet.Cell(3, i + 1);
                    cell.Value = sumHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 242);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int sRow = 4;
                int sIdx = 1;
                foreach (var cls in classes)
                {
                    var activeEnrollments = cls.Enrollments.Where(e => e.Student.IsActive).ToList();
                    var clsEnrIds = activeEnrollments.Select(e => e.Id).ToHashSet();
                    int sunPresentTotal = allAttendances.Count(a => clsEnrIds.Contains(a.EnrollmentId) && a.DayOfWeek == DayOfWeek.Sunday && a.AttendedMass);

                    summarySheet.Cell(sRow, 1).Value = sIdx++;
                    summarySheet.Cell(sRow, 2).Value = SanitizeExcelCell(cls.GradeLevel);
                    summarySheet.Cell(sRow, 3).Value = SanitizeExcelCell(cls.Name);
                    summarySheet.Cell(sRow, 4).Value = SanitizeExcelCell(cls.RoomName);
                    summarySheet.Cell(sRow, 5).Value = activeEnrollments.Count;
                    summarySheet.Cell(sRow, 6).Value = sunPresentTotal;

                    for (int c = 1; c <= 6; c++)
                    {
                        summarySheet.Cell(sRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    sRow++;
                }
                summarySheet.Columns().AdjustToContents();

                // Tạo các Sheet chi tiết cho từng Lớp học
                int sheetCounter = 1;
                foreach (var cls in classes)
                {
                    var safeSheetName = SanitizeWorksheetName($"{sheetCounter++}_{cls.Name}");
                    var ws = workbook.Worksheets.Add(safeSheetName);

                    ws.Cell("A1").Value = $"DANH SÁCH & CHUYÊN CẦN LỚP: {cls.Name.ToUpper()} ({cls.GradeLevel}) - NK: {year.Name}";
                    ws.Cell("A1").Style.Font.Bold = true;
                    ws.Range("A1:H1").Merge();

                    string[] headers = { "STT", "Mã QR", "Tên Thánh", "Họ và Tên", "Phái", "Ngày Sinh", "Số Buổi Lễ CN", "Số Buổi Học GL" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(3, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    var students = cls.Enrollments
                        .Where(e => e.Student.IsActive)
                        .OrderBy(e => e.Student.LastName)
                        .ThenBy(e => e.Student.FirstName)
                        .ToList();

                    int rIdx = 4;
                    int stt = 1;
                    foreach (var en in students)
                    {
                        var stuAtts = allAttendances.Where(a => a.EnrollmentId == en.Id && a.DayOfWeek == DayOfWeek.Sunday).ToList();
                        int massCount = stuAtts.Count(a => a.AttendedMass);
                        int classCount = stuAtts.Count(a => a.ClassAttended);

                        ws.Cell(rIdx, 1).Value = stt++;
                        ws.Cell(rIdx, 2).Value = SanitizeExcelCell(en.Student.StudentCode);
                        ws.Cell(rIdx, 3).Value = SanitizeExcelCell(en.Student.ChristianName);
                        ws.Cell(rIdx, 4).Value = SanitizeExcelCell($"{en.Student.FirstName} {en.Student.LastName}");
                        ws.Cell(rIdx, 5).Value = SanitizeExcelCell(en.Student.Gender);
                        ws.Cell(rIdx, 6).Value = en.Student.DateOfBirth.ToString("dd/MM/yyyy");
                        ws.Cell(rIdx, 7).Value = massCount;
                        ws.Cell(rIdx, 8).Value = classCount;

                        for (int c = 1; c <= 8; c++)
                        {
                            ws.Cell(rIdx, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        }
                        rIdx++;
                    }
                    ws.Columns().AdjustToContents();
                }

                var exportsDir = Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports");
                Directory.CreateDirectory(exportsDir);

                var scopePart = string.IsNullOrWhiteSpace(gradeLevel) || gradeLevel == "ALL" ? "ToanXu" : SanitizeFileNamePart(gradeLevel);
                var fileName = $"BaoCao_GiaoLy_{scopePart}_{DateTime.Now:yyyyMMdd_HHmmss}_{jobTrackerId[..6]}.xlsx";
                var fullPath = Path.Combine(exportsDir, fileName);

                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    workbook.SaveAs(fs);
                }

                await _cache.SetAsync(cacheKey, new BackgroundExportMetaDto
                {
                    JobId = jobTrackerId,
                    Status = "COMPLETED",
                    Title = $"Báo cáo tổng hợp Giáo lý ({(gradeLevel == "ALL" || string.IsNullOrEmpty(gradeLevel) ? "Toàn Giáo Xứ" : gradeLevel)})",
                    FileName = fileName,
                    RequestedBy = requestedBy,
                    CompletedAtUtc = DateTime.UtcNow
                }, TimeSpan.FromHours(24));

                _logger.LogInformation("[Hangfire] Hoàn tất xuất Báo cáo Excel toàn xứ: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Hangfire] Lỗi khi xuất Báo cáo Excel toàn xứ");
                await MarkJobFailedAsync(cacheKey, jobTrackerId, "Lỗi hệ thống khi tổng hợp báo cáo nhiều lớp.");
                throw;
            }
        }

        private async Task MarkJobFailedAsync(string cacheKey, string jobTrackerId, string errorMsg)
        {
            await _cache.SetAsync(cacheKey, new BackgroundExportMetaDto
            {
                JobId = jobTrackerId,
                Status = "FAILED",
                ErrorMessage = errorMsg,
                CompletedAtUtc = DateTime.UtcNow
            }, TimeSpan.FromHours(2));
        }

        private static string SanitizeExcelCell(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var trimmed = text.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@' || trimmed[0] == '\t' || trimmed[0] == '\r'))
            {
                return "'" + trimmed;
            }
            return trimmed;
        }

        private static string SanitizeFileNamePart(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(input.Where(ch => !invalid.Contains(ch)).ToArray());
            return cleaned.Replace(" ", "_");
        }

        private static string SanitizeWorksheetName(string name)
        {
            char[] invalidSheetChars = { ':', '\\', '/', '?', '*', '[', ']' };
            var clean = new string(name.Where(c => !invalidSheetChars.Contains(c)).ToArray());
            return clean.Length > 31 ? clean[..31] : clean;
        }
    }
}