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
            string? fullPath = null;
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

                var safeClassName = SanitizeFileNamePart(targetClass.Name);
                var fileName = $"DanhSach_Lop_{safeClassName}_{VietnamTimeHelper.Now:yyyyMMdd_HHmmss}_{jobTrackerId[..6]}.xlsx";

                var exportsDir = Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports");
                Directory.CreateDirectory(exportsDir);

                fullPath = Path.Combine(exportsDir, fileName);

                await using (var fs = new FileStream(
                    fullPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 65536,
                    options: FileOptions.Asynchronous))
                {
                    if (_studentExcelService is StudentExcelService concreteExcelService)
                    {
                        await concreteExcelService.ExportClassListToStreamAsync(classId, fs);
                    }
                    else
                    {
                        var bytes = await _studentExcelService.ExportClassListToExcelAsync(classId);
                        await fs.WriteAsync(bytes);
                    }
                }

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
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
                }
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
            string? fullPath = null;
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
                    .AsNoTracking()
                    .Where(c => c.AcademicYearId == year.Id);

                if (!string.IsNullOrWhiteSpace(gradeLevel) && gradeLevel != "ALL")
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == gradeLevel);
                }

                var classes = await classesQuery
                    .OrderBy(c => c.GradeLevel)
                    .ThenBy(c => c.Name)
                    .Select(c => new
                    {
                        c.Id,
                        c.GradeLevel,
                        c.Name,
                        c.RoomName
                    })
                    .ToListAsync();

                var classIds = classes.Select(c => c.Id).ToList();

                var activeEnrollments = classIds.Count == 0
                    ? new List<ParishExportStudentRow>()
                    : await _context.Enrollments
                        .AsNoTracking()
                        .Where(e => classIds.Contains(e.ClassRoomId) && e.Student.IsActive)
                        .OrderBy(e => e.Student.LastName)
                        .ThenBy(e => e.Student.FirstName)
                        .Select(e => new ParishExportStudentRow(
                            e.Id,
                            e.ClassRoomId,
                            e.Student.StudentCode,
                            e.Student.ChristianName,
                            e.Student.FirstName,
                            e.Student.LastName,
                            e.Student.Gender,
                            e.Student.DateOfBirth))
                        .ToListAsync();

                var attendanceStats = classIds.Count == 0
                    ? new List<ParishAttendanceSummaryRow>()
                    : await _context.Attendances
                        .AsNoTracking()
                        .Where(a => a.DayOfWeek == DayOfWeek.Sunday
                            && (a.AttendedMass || a.ClassAttended)
                            && classIds.Contains(a.Enrollment.ClassRoomId)
                            && a.Enrollment.Student.IsActive)
                        .GroupBy(a => new { a.EnrollmentId, a.Enrollment.ClassRoomId })
                        .Select(g => new ParishAttendanceSummaryRow(
                            g.Key.EnrollmentId,
                            g.Key.ClassRoomId,
                            g.Sum(a => a.AttendedMass ? 1 : 0),
                            g.Sum(a => a.ClassAttended ? 1 : 0)))
                        .ToListAsync();

                var enrollmentsByClass = activeEnrollments
                    .GroupBy(e => e.ClassRoomId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var enrollmentCounts = attendanceStats
                    .ToDictionary(x => x.EnrollmentId, x => (x.MassCount, x.ClassCount));

                var classSundayMassCounts = attendanceStats
                    .GroupBy(x => x.ClassRoomId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.MassCount));

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
                    int activeCount = enrollmentsByClass.TryGetValue(cls.Id, out var clsStudents) ? clsStudents.Count : 0;
                    int sunPresentTotal = classSundayMassCounts.GetValueOrDefault(cls.Id, 0);

                    summarySheet.Cell(sRow, 1).Value = sIdx++;
                    summarySheet.Cell(sRow, 2).Value = SanitizeExcelCell(cls.GradeLevel);
                    summarySheet.Cell(sRow, 3).Value = SanitizeExcelCell(cls.Name);
                    summarySheet.Cell(sRow, 4).Value = SanitizeExcelCell(cls.RoomName);
                    summarySheet.Cell(sRow, 5).Value = activeCount;
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

                    var students = enrollmentsByClass.TryGetValue(cls.Id, out var clsList)
                        ? clsList
                        : new List<ParishExportStudentRow>();

                    int rIdx = 4;
                    int stt = 1;
                    foreach (var en in students)
                    {
                        var (massCount, classCount) = enrollmentCounts.GetValueOrDefault(en.EnrollmentId, (0, 0));

                        ws.Cell(rIdx, 1).Value = stt++;
                        ws.Cell(rIdx, 2).Value = SanitizeExcelCell(en.StudentCode);
                        ws.Cell(rIdx, 3).Value = SanitizeExcelCell(en.ChristianName);
                        ws.Cell(rIdx, 4).Value = SanitizeExcelCell($"{en.FirstName} {en.LastName}");
                        ws.Cell(rIdx, 5).Value = SanitizeExcelCell(en.Gender);
                        ws.Cell(rIdx, 6).Value = en.DateOfBirth.ToString("dd/MM/yyyy");
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
                var fileName = $"BaoCao_GiaoLy_{scopePart}_{VietnamTimeHelper.Now:yyyyMMdd_HHmmss}_{jobTrackerId[..6]}.xlsx";
                fullPath = Path.Combine(exportsDir, fileName);

                await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, options: FileOptions.Asynchronous))
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
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
                }
                _logger.LogError(ex, "[Hangfire] Lỗi khi xuất Báo cáo Excel toàn xứ");
                await MarkJobFailedAsync(cacheKey, jobTrackerId, "Lỗi hệ thống khi tổng hợp báo cáo nhiều lớp.");
                throw;
            }
        }

        private sealed record ParishExportStudentRow(
            long EnrollmentId,
            int ClassRoomId,
            string StudentCode,
            string ChristianName,
            string FirstName,
            string LastName,
            string Gender,
            DateTime DateOfBirth);

        private sealed record ParishAttendanceSummaryRow(
            long EnrollmentId,
            int ClassRoomId,
            int MassCount,
            int ClassCount);

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