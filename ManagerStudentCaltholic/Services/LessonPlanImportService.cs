using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace ManagerStudentCaltholic.Services
{
    public class LessonPlanImportService : ILessonPlanImportService
    {
        private readonly ILogger<LessonPlanImportService> _logger;

        public LessonPlanImportService(ILogger<LessonPlanImportService> logger)
        {
            _logger = logger;
        }

        public async Task<List<ClassLessonPlan>> ParseFileAsync(IFormFile file, int classRoomId, int academicYearId)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            return ext switch
            {
                ".xlsx" or ".xls" => ParseExcel(stream, classRoomId, academicYearId),
                ".docx" => ParseWord(stream, classRoomId, academicYearId),
                ".pdf" => ParsePdf(stream, classRoomId, academicYearId),
                _ => throw new NotSupportedException($"Định dạng file '{ext}' chưa được hỗ trợ. Vui lòng chọn .xlsx, .docx hoặc .pdf.")
            };
        }

        /// <summary>
        /// 1. ĐỌC FILE EXCEL (.XLSX, .XLS)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="classRoomId"></param>
        /// <param name="academicYearId"></param>
        /// <returns></returns>
        private List<ClassLessonPlan> ParseExcel(Stream stream, int classRoomId, int academicYearId)
        {
            var plans = new List<ClassLessonPlan>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return plans;

            var rows = worksheet.RangeUsed()?.RowsUsed()?.Skip(1);
            if (rows == null) return plans;

            foreach (var row in rows)
            {
                var rawDate = row.Cell(1).GetString().Trim();
                var topic = row.Cell(3).GetString().Trim();

                if (string.IsNullOrWhiteSpace(rawDate) && string.IsNullOrWhiteSpace(topic)) continue;

                var (lessonDate, liturgical) = ExtractDateAndLiturgical(rawDate);
                if (!lessonDate.HasValue) continue;

                var lessonCode = row.Cell(2).GetString().Trim();
                var keyPoints = row.Cell(4).GetString().Trim();
                var teacherName = row.Cell(5).GetString().Trim();
                var notes = row.Cell(6).GetString().Trim();

                plans.Add(CreatePlanEntity(classRoomId, academicYearId, lessonDate.Value, liturgical, lessonCode, topic, keyPoints, teacherName, notes));
            }
            return plans;
        }

        /// <summary>
        /// 2. ĐỌC FILE WORD (.DOCX)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="classRoomId"></param>
        /// <param name="academicYearId"></param>
        /// <returns></returns>
        private List<ClassLessonPlan> ParseWord(Stream stream, int classRoomId, int academicYearId)
        {
            var plans = new List<ClassLessonPlan>();
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return plans;

            // Ưu tiên đọc từ Bảng (Table) trong file Word
            var tables = body.Elements<Table>();
            foreach (var table in tables)
            {
                var rows = table.Elements<TableRow>().Skip(1);
                foreach (var row in rows)
                {
                    var cells = row.Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();
                    if (cells.Count < 2) continue;

                    var rawDate = cells[0];
                    var (lessonDate, liturgical) = ExtractDateAndLiturgical(rawDate);
                    if (!lessonDate.HasValue) continue;

                    var lessonCode = cells.Count > 1 ? cells[1] : "";
                    var topic = cells.Count > 2 ? cells[2] : "";
                    var keyPoints = cells.Count > 3 ? cells[3] : "";
                    var teacher = cells.Count > 4 ? cells[4] : "";
                    var notes = cells.Count > 5 ? cells[5] : "";

                    plans.Add(CreatePlanEntity(classRoomId, academicYearId, lessonDate.Value, liturgical, lessonCode, topic, keyPoints, teacher, notes));
                }
            }
            return plans;
        }

        /// <summary>
        /// 3. ĐỌC FILE PDF (.PDF)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="classRoomId"></param>
        /// <param name="academicYearId"></param>
        /// <returns></returns>
        private List<ClassLessonPlan> ParsePdf(Stream stream, int classRoomId, int academicYearId)
        {
            var plans = new List<ClassLessonPlan>();
            using var document = PdfDocument.Open(stream);

            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // Tìm định dạng ngày dd/MM/yyyy trong từng dòng
                    var match = Regex.Match(line, @"(\d{1,2}/\d{1,2}/\d{4})");
                    if (match.Success)
                    {
                        var rawDate = match.Groups[1].Value;
                        if (DateTime.TryParseExact(rawDate, new[] { "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        {
                            var remaining = line.Replace(rawDate, "").Trim();
                            plans.Add(CreatePlanEntity(classRoomId, academicYearId, parsedDate, "Chúa Nhật", "", remaining, "", "", "Nạp từ file PDF"));
                        }
                    }
                }
            }
            return plans;
        }

        // ==========================================
        // HELPER FUNCTIONS
        // ==========================================
        private static (DateTime? Date, string? Liturgical) ExtractDateAndLiturgical(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);

            var dateMatch = Regex.Match(raw, @"(\d{1,2}/\d{1,2}/\d{4})");
            if (dateMatch.Success && DateTime.TryParseExact(dateMatch.Groups[1].Value, new[] { "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                var liturgical = raw.Replace(dateMatch.Groups[1].Value, "").Trim();
                return (dt, string.IsNullOrWhiteSpace(liturgical) ? null : liturgical);
            }
            return (null, raw);
        }

        private static ClassLessonPlan CreatePlanEntity(int classId, int yearId, DateTime date, string? liturgical, string? code, string topic, string? keyPoints, string? teacher, string? notes)
        {
            var upperTopic = (topic + " " + code).ToUpperInvariant();
            bool isExam = upperTopic.Contains("KT") || upperTopic.Contains("KIỂM TRA") || upperTopic.Contains("THI HK") || upperTopic.Contains("THI HỌC KỲ");
            bool isOff = upperTopic.Contains("NGHỈ") || upperTopic.Contains("TRUNG THU") || upperTopic.Contains("TẾT");

            return new ClassLessonPlan
            {
                ClassRoomId = classId,
                AcademicYearId = yearId,
                LessonDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                LiturgicalDay = liturgical,
                LessonCode = code,
                TopicTitle = string.IsNullOrWhiteSpace(topic) ? (code ?? "Kế hoạch tuần") : topic,
                KeyPoints = keyPoints,
                AssignedTeacherName = teacher,
                EventNotes = notes,
                IsExamDay = isExam,
                IsDayOff = isOff
            };
        }
    }
}