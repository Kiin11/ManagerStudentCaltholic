using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public interface IStudentExcelService
    {
        byte[] GenerateTemplateFile();
        Task<StudentImportResultDto> ImportStudentsFromExcelAsync(Stream fileStream);
        Task<byte[]> ExportClassListToExcelAsync(int classId);
    }

    public class StudentExcelService : IStudentExcelService
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeGenerator _codeGenerator;
        private readonly ILogger<StudentExcelService> _logger;

        public StudentExcelService(
            ParishDbContext context,
            IStudentCodeGenerator codeGenerator,
            ILogger<StudentExcelService> logger)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        // ==========================================
        // 1. TẠO FILE EXCEL MẪU CHO NHẬP LIỆU
        // ==========================================
        public byte[] GenerateTemplateFile()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachThieuNhi");

            // Tiêu đề cột
            string[] headers = {
                "Mã QR / Định danh (để trống tự sinh)",
                "Tên Thánh (*)",
                "Họ & Tên Đệm (*)",
                "Tên (*)",
                "Giới Tính (Nam/Nữ)",
                "Ngày Sinh (dd/MM/yyyy) (*)",
                "Ngày Rửa Tội (dd/MM/yyyy)",
                "Ngày Thêm Sức (dd/MM/yyyy)",
                "SĐT Phụ Huynh"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 242);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Dòng dữ liệu ví dụ mẫu
            worksheet.Cell(2, 1).Value = "TN-2026-0001";
            worksheet.Cell(2, 2).Value = "Giuse";
            worksheet.Cell(2, 3).Value = "Nguyễn Văn";
            worksheet.Cell(2, 4).Value = "An";
            worksheet.Cell(2, 5).Value = "Nam";
            worksheet.Cell(2, 6).Value = "15/05/2014";
            worksheet.Cell(2, 7).Value = "20/06/2014";
            worksheet.Cell(2, 8).Value = "";
            worksheet.Cell(2, 9).Value = "0912345678";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ==========================================
        // 2. BATCH IMPORT TỪ EXCEL (TASK-308)
        // ==========================================
        public async Task<StudentImportResultDto> ImportStudentsFromExcelAsync(Stream fileStream)
        {
            var result = new StudentImportResultDto();
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                result.ErrorMessages.Add("File Excel không chứa bất kỳ trang tính nào.");
                return result;
            }

            var rows = worksheet.RangeUsed()?.RowsUsed()?.Skip(1); // Bỏ qua tiêu đề
            if (rows == null || !rows.Any())
            {
                result.ErrorMessages.Add("File Excel không có dữ liệu để nạp.");
                return result;
            }

            var codeList = await _context.Students
                .Select(s => s.StudentCode.ToLower())
                .ToListAsync();

            var existingCodes = new HashSet<string>(codeList);

            var currentYear = DateTime.UtcNow.Year;
            var prefix = $"TN-{currentYear}-";
            var maxSeq = await _context.Students
                .Where(s => s.StudentCode.StartsWith(prefix))
                .OrderByDescending(s => s.StudentCode)
                .Select(s => s.StudentCode)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (!string.IsNullOrEmpty(maxSeq) && maxSeq.Length >= prefix.Length + 4)
            {
                var seqPart = maxSeq.Substring(prefix.Length);
                if (int.TryParse(seqPart, out int parsed)) nextSeq = parsed + 1;
            }

            var studentsToInsert = new List<Student>();
            int rowIndex = 1;

            foreach (var row in rows)
            {
                rowIndex++;
                var code = row.Cell(1).GetString().Trim();
                var christian = row.Cell(2).GetString().Trim();
                var first = row.Cell(3).GetString().Trim();
                var last = row.Cell(4).GetString().Trim();
                var gender = row.Cell(5).GetString().Trim();
                var dobRaw = row.Cell(6).GetString().Trim();
                var baptismRaw = row.Cell(7).GetString().Trim();
                var confirmRaw = row.Cell(8).GetString().Trim();
                var phone = row.Cell(9).GetString().Trim();

                // Kiểm tra dòng trống
                if (string.IsNullOrWhiteSpace(christian) && string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
                    continue;

                result.TotalRows++;

                if (string.IsNullOrWhiteSpace(christian) || string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
                {
                    result.ErrorMessages.Add($"Dòng {rowIndex}: Thiếu thông tin bắt buộc (Tên thánh, Họ đệm hoặc Tên).");
                    result.FailureCount++;
                    continue;
                }


                // 1. Ngày sinh (bắt buộc)
                if (!TryParseCellDate(row.Cell(6), out DateTime dob))
                {
                    result.ErrorMessages.Add($"Dòng {rowIndex}: Ngày sinh '{row.Cell(6).GetString()}' không hợp lệ (bắt buộc định dạng dd/MM/yyyy).");
                    result.FailureCount++;
                    continue;
                }

                // 2. Ngày Rửa tội (không bắt buộc)
                DateTime? baptismDate = TryParseCellDate(row.Cell(7), out var bDate) ? bDate : null;

                // 3. Ngày Thêm sức (không bắt buộc)
                DateTime? confirmDate = TryParseCellDate(row.Cell(8), out var cDate) ? cDate : null;

                // Tự sinh mã nếu để trống
                if (string.IsNullOrWhiteSpace(code))
                {
                    code = $"{prefix}{nextSeq++:D4}";
                }
                else
                {
                    if (existingCodes.Contains(code.ToLower()))
                    {
                        result.ErrorMessages.Add($"Dòng {rowIndex}: Mã định danh '{code}' đã tồn tại trên hệ thống.");
                        result.FailureCount++;
                        continue;
                    }
                }
                existingCodes.Add(code.ToLower());

                studentsToInsert.Add(new Student
                {
                    StudentCode = code.ToUpper(),
                    ChristianName = christian,
                    FirstName = first,
                    LastName = last,
                    Gender = string.Equals(gender, "Nữ", StringComparison.OrdinalIgnoreCase) ? "Nữ" : "Nam",
                    DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc),
                    BaptismDate = baptismDate.HasValue ? DateTime.SpecifyKind(baptismDate.Value, DateTimeKind.Utc) : null,
                    ConfirmationDate = confirmDate.HasValue ? DateTime.SpecifyKind(confirmDate.Value, DateTimeKind.Utc) : null,
                    ParentPhone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    IsActive = true
                });
            }

            if (studentsToInsert.Any())
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    await _context.Students.AddRangeAsync(studentsToInsert);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                result.SuccessCount = studentsToInsert.Count;
                _logger.LogInformation("Import thành công {Count} hồ sơ thiếu nhi từ file Excel", studentsToInsert.Count);
            }

            return result;
        }

        // ==========================================
        // 3. XUẤT DANH SÁCH LỚP KHỔ A4 KÈM KHUNG CHỮ KÝ (TASK-309)
        // ==========================================
        public async Task<byte[]> ExportClassListToExcelAsync(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null) throw new Exception("Không tìm thấy thông tin lớp học");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(targetClass.Name);

            // Thiết lập trang in A4 dọc
            worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            worksheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            worksheet.PageSetup.Margins.SetTop(0.5);
            worksheet.PageSetup.Margins.SetBottom(0.5);
            worksheet.PageSetup.Margins.SetLeft(0.5);
            worksheet.PageSetup.Margins.SetRight(0.5);

            // Header thông tin giáo xứ
            worksheet.Cell("A1").Value = "GIÁO ĐOÀN THIẾU NHI THÁNH THỂ";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 11;

            worksheet.Cell("A2").Value = $"LỚP: {targetClass.Name.ToUpper()} - NIÊN KHÓA: {targetClass.AcademicYear.Name}";
            worksheet.Cell("A2").Style.Font.Bold = true;
            worksheet.Cell("A2").Style.Font.FontSize = 13;
            worksheet.Range("A2:G2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A3").Value = $"Khối: {targetClass.GradeLevel} | Sĩ số: {targetClass.Enrollments.Count} em";
            worksheet.Range("A3:G3").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Bảng danh sách cột
            string[] tableHeaders = { "STT", "Mã QR", "Tên Thánh", "Họ và Tên", "Phái", "Ngày Sinh", "Ghi Chú / Chữ Ký" };
            int startRow = 5;

            for (int i = 0; i < tableHeaders.Length; i++)
            {
                var cell = worksheet.Cell(startRow, i + 1);
                cell.Value = tableHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 240, 240);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            var students = targetClass.Enrollments
                .Select(e => e.Student)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList();

            int currRow = startRow + 1;
            int stt = 1;
            foreach (var s in students)
            {
                worksheet.Cell(currRow, 1).Value = stt++;
                worksheet.Cell(currRow, 2).Value = s.StudentCode;
                worksheet.Cell(currRow, 3).Value = s.ChristianName;
                worksheet.Cell(currRow, 4).Value = $"{s.FirstName} {s.LastName}".Trim();
                worksheet.Cell(currRow, 5).Value = s.Gender;
                worksheet.Cell(currRow, 6).Value = s.DateOfBirth.ToString("dd/MM/yyyy");
                worksheet.Cell(currRow, 7).Value = "";

                worksheet.Cell(currRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                for (int c = 1; c <= 7; c++)
                {
                    worksheet.Cell(currRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currRow++;
            }

            // Khung chữ ký cuối trang
            currRow += 2;
            worksheet.Cell(currRow, 2).Value = "GIÁO LÝ VIÊN CHỦ NHIỆM";
            worksheet.Cell(currRow, 2).Style.Font.Bold = true;
            worksheet.Cell(currRow, 6).Value = "BAN HÀNH GIÁO / CHA XỨ";
            worksheet.Cell(currRow, 6).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Parse ngày từ ô Excel, hỗ trợ nhiều định dạng ngày khác nhau.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="parsedDate"></param>
        /// <returns></returns>
        private static bool TryParseCellDate(IXLCell cell, out DateTime parsedDate)
        {
            parsedDate = default;

            // Nếu ô Excel được nhận diện sẵn là kiểu DateTime
            if (cell.DataType == XLDataType.DateTime)
            {
                parsedDate = cell.GetDateTime();
                return true;
            }

            var text = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Các format ngày chấp nhận
            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy", "yyyy-MM-dd" };

            return DateTime.TryParseExact(text, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate);
        }
    }
}