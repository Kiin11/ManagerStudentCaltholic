using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace ManagerStudentCaltholic.Services
{
    public class ClassCurriculumService : IClassCurriculumService
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<ClassCurriculumService> _logger;
        private readonly string _storageFolder;

        public ClassCurriculumService(ParishDbContext context, ILogger<ClassCurriculumService> logger)
        {
            _context = context;
            _logger = logger;
            _storageFolder = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads", "curriculums"));
            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }
        }

        public async Task<ClassCurriculumDocumentDto?> GetCurriculumByClassAsync(int classRoomId, int academicYearId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classRoomId);
            if (cls == null) return null;

            var year = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == academicYearId);

            var doc = await _context.ClassCurriculumDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ClassRoomId == classRoomId && d.AcademicYearId == academicYearId);

            var items = new List<CurriculumItemDto>();

            if (doc != null && !string.IsNullOrWhiteSpace(doc.ContentJson))
            {
                try
                {
                    items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(doc.ContentJson) ?? new List<CurriculumItemDto>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể phân tích ContentJson của CurriculumDocument Id: {Id}", doc.Id);
                }
            }

            // Nếu chưa có ContentJson trong Document, nạp trực tiếp từ bảng ClassLessonPlans hiện hữu
            if (!items.Any())
            {
                var lessonPlans = await _context.ClassLessonPlans
                    .Where(p => p.ClassRoomId == classRoomId && p.AcademicYearId == academicYearId)
                    .OrderBy(p => p.LessonDate)
                    .AsNoTracking()
                    .ToListAsync();

                int week = 1;
                foreach (var lp in lessonPlans)
                {
                    items.Add(new CurriculumItemDto
                    {
                        WeekNumber = lp.WeekNumber ?? week++,
                        LessonDate = lp.LessonDate,
                        LessonDateStr = lp.LessonDate.ToString("dd/MM/yyyy"),
                        LiturgicalDay = lp.LiturgicalDay,
                        LessonCode = lp.LessonCode,
                        TopicTitle = lp.TopicTitle,
                        KeyPoints = lp.KeyPoints,
                        AssignedTeacherName = lp.AssignedTeacherName,
                        AssignedTeacherUserId = lp.AssignedTeacherUserId,
                        EventNotes = lp.EventNotes,
                        IsExamDay = lp.IsExamDay,
                        IsDayOff = lp.IsDayOff,
                        Semester = lp.Semester ?? 1
                    });
                }
            }

            return new ClassCurriculumDocumentDto
            {
                Id = doc?.Id ?? 0,
                ClassRoomId = classRoomId,
                ClassName = cls.Name,
                GradeLevel = cls.GradeLevel,
                AcademicYearId = academicYearId,
                AcademicYearName = year?.Name ?? "Niên khóa hiện hành",
                Title = doc?.Title ?? $"Kế hoạch năm học Lớp {cls.Name}",
                Description = doc?.Description,
                CurrentVersion = doc?.CurrentVersion ?? "v1.0",
                VersionNumber = doc?.VersionNumber ?? 1,
                LastAttachmentFileName = doc?.LastAttachmentFileName,
                LastAttachmentFilePath = doc?.LastAttachmentFilePath,
                LastAttachmentFileSize = doc?.LastAttachmentFileSize ?? 0,
                UpdatedBy = doc?.UpdatedBy ?? "Hệ thống",
                UpdatedAt = doc?.UpdatedAt ?? DateTime.UtcNow,
                TotalWeeks = items.Count,
                Items = items.OrderBy(i => i.WeekNumber).ToList()
            };
        }

        public async Task<List<ClassCurriculumVersionDto>> GetVersionsAsync(long documentId)
        {
            var versions = await _context.ClassCurriculumVersions
                .Where(v => v.ClassCurriculumDocumentId == documentId)
                .OrderByDescending(v => v.VersionNumber)
                .AsNoTracking()
                .ToListAsync();

            return versions.Select(v =>
            {
                int totalWeeks = 0;
                if (!string.IsNullOrWhiteSpace(v.ContentJson))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<CurriculumItemDto>>(v.ContentJson);
                        totalWeeks = parsed?.Count ?? 0;
                    }
                    catch { }
                }

                return new ClassCurriculumVersionDto
                {
                    Id = v.Id,
                    ClassCurriculumDocumentId = v.ClassCurriculumDocumentId,
                    VersionNumber = v.VersionNumber,
                    VersionTag = v.VersionTag,
                    ChangeSummary = v.ChangeSummary,
                    FileName = v.FileName,
                    FilePath = $"/LessonPlans/DownloadCurriculumVersion/{v.Id}",
                    FileSize = v.FileSize,
                    CreatedBy = v.CreatedBy,
                    CreatedAt = v.CreatedAt,
                    TotalWeeks = totalWeeks
                };
            }).ToList();
        }

        public async Task<ClassCurriculumVersionDto?> GetVersionDetailAsync(long versionId)
        {
            var v = await _context.ClassCurriculumVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == versionId);
            if (v == null) return null;

            List<CurriculumItemDto> items = new();
            if (!string.IsNullOrWhiteSpace(v.ContentJson))
            {
                try
                {
                    items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(v.ContentJson) ?? new();
                }
                catch { }
            }

            return new ClassCurriculumVersionDto
            {
                Id = v.Id,
                ClassCurriculumDocumentId = v.ClassCurriculumDocumentId,
                VersionNumber = v.VersionNumber,
                VersionTag = v.VersionTag,
                ChangeSummary = v.ChangeSummary,
                FileName = v.FileName,
                FilePath = $"/LessonPlans/DownloadCurriculumVersion/{v.Id}",
                FileSize = v.FileSize,
                CreatedBy = v.CreatedBy,
                CreatedAt = v.CreatedAt,
                TotalWeeks = items.Count,
                Items = items
            };
        }

        public async Task<CurriculumParseResultDto> ParseUploadedCurriculumFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new CurriculumParseResultDto
                {
                    Success = false,
                    Message = "Không tìm thấy file để nạp chương trình."
                };
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".xlsx", ".xls", ".docx", ".csv", ".md", ".pdf" };
            if (!allowed.Contains(ext))
            {
                return new CurriculumParseResultDto
                {
                    Success = false,
                    Message = $"Định dạng '{ext}' không được hỗ trợ. Vui lòng chọn .xlsx, .docx, .csv, .md hoặc .pdf."
                };
            }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                List<CurriculumItemDto> items = ext switch
                {
                    ".xlsx" or ".xls" => ParseExcelStream(stream),
                    ".docx" => ParseWordStream(stream),
                    ".csv" => ParseCsvStream(stream),
                    ".md" => ParseMarkdownStream(stream),
                    ".pdf" => ParsePdfStream(stream),
                    _ => new List<CurriculumItemDto>()
                };

                // Chuẩn hóa số thứ tự tuần nếu chưa có
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].WeekNumber <= 0)
                    {
                        items[i].WeekNumber = i + 1;
                    }
                }

                return new CurriculumParseResultDto
                {
                    Success = true,
                    Message = $"Nạp thành công {items.Count} tuần học từ file '{file.FileName}'!",
                    OriginalFileName = file.FileName,
                    TotalItemsParsed = items.Count,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích file kế hoạch {FileName}", file.FileName);
                return new CurriculumParseResultDto
                {
                    Success = false,
                    Message = $"Lỗi khi đọc file: {ex.Message}"
                };
            }
        }

        public async Task<(bool Success, string Message, ClassCurriculumDocumentDto? Data)> SaveCurriculumAsync(
            SaveCurriculumRequestDto request, long userId, string userName)
        {
            if (request == null || request.ClassRoomId <= 0)
            {
                return (false, "Lớp học không hợp lệ.", null);
            }

            var cls = await _context.Classes.FindAsync(request.ClassRoomId);
            if (cls == null) return (false, "Không tìm thấy lớp học.", null);

            var academicYearId = request.AcademicYearId ?? cls.AcademicYearId;
            var year = await _context.AcademicYears.FindAsync(academicYearId);
            if (year == null) return (false, "Không tìm thấy niên khóa.", null);

            var items = request.Items ?? new List<CurriculumItemDto>();
            // Chuẩn hóa và làm sạch dữ liệu tuần học
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.WeekNumber <= 0) item.WeekNumber = i + 1;

                if (!item.LessonDate.HasValue && !string.IsNullOrWhiteSpace(item.LessonDateStr))
                {
                    if (DateTime.TryParseExact(item.LessonDateStr.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        item.LessonDate = DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
                    }
                }

                if (string.IsNullOrWhiteSpace(item.TopicTitle))
                {
                    item.TopicTitle = !string.IsNullOrWhiteSpace(item.LessonCode) ? item.LessonCode : $"Tuần {item.WeekNumber}";
                }

                var text = (item.TopicTitle + " " + item.LessonCode).ToUpperInvariant();
                if (text.Contains("KT") || text.Contains("KIỂM TRA") || text.Contains("THI HK") || text.Contains("THI HỌC KỲ"))
                {
                    item.IsExamDay = true;
                }
                if (text.Contains("NGHỈ") || text.Contains("TẾT") || text.Contains("TRUNG THU") || text.Contains("HỘI CHỢ"))
                {
                    item.IsDayOff = true;
                }
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var doc = await _context.ClassCurriculumDocuments
                        .FirstOrDefaultAsync(d => d.ClassRoomId == request.ClassRoomId && d.AcademicYearId == academicYearId);

                    int nextVersionNum = (doc?.VersionNumber ?? 0) + 1;
                    string versionTag = $"v{nextVersionNum}";

                    string cleanClassName = Regex.Replace(cls.Name, @"[^a-zA-Z0-9_\-]", "");
                    if (string.IsNullOrWhiteSpace(cleanClassName)) cleanClassName = $"Class{cls.Id}";

                    string dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
                    string generatedFileName = $"KeHoach_{cleanClassName}_{versionTag}_{dateStamp}.xlsx";
                    string storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.xlsx";
                    string physicalPath = Path.Combine(_storageFolder, storedFileName);

                    // 1. Tự động sinh file Excel với mẫu chuẩn giao diện đẹp
                    var excelBytes = GenerateExcelFileBytes(cls.Name, cls.GradeLevel, year.Name, versionTag, items);
                    await File.WriteAllBytesAsync(physicalPath, excelBytes);

                    string contentJson = JsonSerializer.Serialize(items);

                    if (doc == null)
                    {
                        doc = new ClassCurriculumDocument
                        {
                            ClassRoomId = cls.Id,
                            AcademicYearId = academicYearId,
                            Title = string.IsNullOrWhiteSpace(request.Title) ? $"Kế hoạch năm học Lớp {cls.Name}" : request.Title.Trim(),
                            Description = request.Description,
                            CurrentVersion = versionTag,
                            VersionNumber = nextVersionNum,
                            ContentJson = contentJson,
                            LastAttachmentFileName = generatedFileName,
                            LastAttachmentFilePath = string.Empty, // Sẽ gán sau khi có ID version
                            LastAttachmentFileSize = excelBytes.Length,
                            LastAttachmentContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            CreatedBy = userName,
                            CreatedByUserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedBy = userName,
                            UpdatedByUserId = userId,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.ClassCurriculumDocuments.Add(doc);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        doc.VersionNumber = nextVersionNum;
                        doc.CurrentVersion = versionTag;
                        doc.ContentJson = contentJson;
                        doc.LastAttachmentFileName = generatedFileName;
                        doc.LastAttachmentFileSize = excelBytes.Length;
                        doc.LastAttachmentContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        doc.UpdatedBy = userName;
                        doc.UpdatedByUserId = userId;
                        doc.UpdatedAt = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(request.Title)) doc.Title = request.Title.Trim();
                        if (request.Description != null) doc.Description = request.Description.Trim();
                        await _context.SaveChangesAsync();
                    }

                    // 2. Tạo bản ghi ClassCurriculumVersion lưu vết lịch sử
                    var versionEntity = new ClassCurriculumVersion
                    {
                        ClassCurriculumDocumentId = doc.Id,
                        VersionNumber = nextVersionNum,
                        VersionTag = versionTag,
                        ChangeSummary = string.IsNullOrWhiteSpace(request.ChangeSummary) ? "Cập nhật kế hoạch trực tuyến" : request.ChangeSummary.Trim(),
                        ContentJson = contentJson,
                        FileName = generatedFileName,
                        StoredFileName = storedFileName,
                        FilePath = $"/LessonPlans/DownloadCurriculumVersion/{0}", // Cập nhật ngay sau SaveChanges
                        FileSize = excelBytes.Length,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        CreatedBy = userName,
                        CreatedByUserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ClassCurriculumVersions.Add(versionEntity);
                    await _context.SaveChangesAsync();

                    // Cập nhật FilePath chính xác với ID
                    versionEntity.FilePath = $"/LessonPlans/DownloadCurriculumVersion/{versionEntity.Id}";
                    doc.LastAttachmentFilePath = versionEntity.FilePath;
                    await _context.SaveChangesAsync();

                    // 3. Đồng bộ hai chiều sang ClassLessonPlans (Sub-task 07.1.2)
                    var existingLessonPlans = await _context.ClassLessonPlans
                        .Where(p => p.ClassRoomId == cls.Id && p.AcademicYearId == academicYearId)
                        .ToListAsync();

                    // Xóa các kế hoạch cũ để thay thế bằng danh sách mới nhất
                    _context.ClassLessonPlans.RemoveRange(existingLessonPlans);

                    foreach (var it in items)
                    {
                        DateTime lDate = it.LessonDate.HasValue
                            ? DateTime.SpecifyKind(it.LessonDate.Value.Date, DateTimeKind.Utc)
                            : DateTime.UtcNow.Date;

                        _context.ClassLessonPlans.Add(new ClassLessonPlan
                        {
                            ClassRoomId = cls.Id,
                            AcademicYearId = academicYearId,
                            CurriculumDocumentId = doc.Id,
                            WeekNumber = it.WeekNumber,
                            Semester = it.Semester > 0 ? it.Semester : 1,
                            LessonDate = lDate,
                            LiturgicalDay = it.LiturgicalDay,
                            LessonCode = it.LessonCode,
                            TopicTitle = it.TopicTitle,
                            KeyPoints = it.KeyPoints,
                            AssignedTeacherName = it.AssignedTeacherName,
                            AssignedTeacherUserId = it.AssignedTeacherUserId,
                            EventNotes = it.EventNotes,
                            IsExamDay = it.IsExamDay,
                            IsDayOff = it.IsDayOff
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Người dùng {User} đã lưu Kế hoạch lớp {ClassName} phiên bản {Version} ({FileName})",
                        userName, cls.Name, versionTag, generatedFileName);

                    var resultDto = await GetCurriculumByClassAsync(cls.Id, academicYearId);
                    return (true, $"Đã lưu kế hoạch lớp thành công phiên bản {versionTag} và tạo file '{generatedFileName}'!", resultDto);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi lưu kế hoạch lớp {ClassId}", request.ClassRoomId);
                    return (false, $"Lỗi hệ thống khi lưu kế hoạch: {ex.Message}", null);
                }
            });
        }

        public async Task<(bool Success, string Message, ClassCurriculumDocumentDto? Data)> RollbackCurriculumAsync(
            long documentId, long versionId, long userId, string userName)
        {
            var doc = await _context.ClassCurriculumDocuments.FindAsync(documentId);
            if (doc == null) return (false, "Không tìm thấy tài liệu kế hoạch lớp.", null);

            var ver = await _context.ClassCurriculumVersions
                .FirstOrDefaultAsync(v => v.Id == versionId && v.ClassCurriculumDocumentId == documentId);
            if (ver == null) return (false, "Không tìm thấy phiên bản cần khôi phục.", null);

            List<CurriculumItemDto> items = new();
            if (!string.IsNullOrWhiteSpace(ver.ContentJson))
            {
                try
                {
                    items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(ver.ContentJson) ?? new();
                }
                catch (Exception ex)
                {
                    return (false, $"Không thể đọc dữ liệu bản chụp của phiên bản: {ex.Message}", null);
                }
            }

            var saveRequest = new SaveCurriculumRequestDto
            {
                ClassRoomId = doc.ClassRoomId,
                AcademicYearId = doc.AcademicYearId,
                Title = doc.Title,
                Description = doc.Description,
                ChangeSummary = $"Khôi phục về {ver.VersionTag} (Lưu lúc {ver.CreatedAt:dd/MM/yyyy HH:mm} bởi {ver.CreatedBy})",
                Items = items
            };

            return await SaveCurriculumAsync(saveRequest, userId, userName);
        }

        public async Task<(byte[] FileBytes, string ContentType, string DownloadFileName)> ExportCurriculumToExcelAsync(
            long documentId, long? versionId = null)
        {
            var doc = await _context.ClassCurriculumDocuments
                .Include(d => d.ClassRoom)
                .Include(d => d.AcademicYear)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null)
            {
                throw new FileNotFoundException("Không tìm thấy tài liệu kế hoạch.");
            }

            List<CurriculumItemDto> items = new();
            string versionTag = doc.CurrentVersion;

            if (versionId.HasValue && versionId.Value > 0)
            {
                var ver = await _context.ClassCurriculumVersions.FindAsync(versionId.Value);
                if (ver != null)
                {
                    versionTag = ver.VersionTag;
                    if (!string.IsNullOrWhiteSpace(ver.ContentJson))
                    {
                        try { items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(ver.ContentJson) ?? new(); } catch { }
                    }
                }
            }

            if (!items.Any() && !string.IsNullOrWhiteSpace(doc.ContentJson))
            {
                try { items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(doc.ContentJson) ?? new(); } catch { }
            }

            string cleanClassName = Regex.Replace(doc.ClassRoom?.Name ?? "Lop", @"[^a-zA-Z0-9_\-]", "");
            string downloadName = $"KeHoach_{cleanClassName}_{versionTag}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            var bytes = GenerateExcelFileBytes(doc.ClassRoom?.Name ?? "", doc.ClassRoom?.GradeLevel ?? "", doc.AcademicYear?.Name ?? "", versionTag, items);

            return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", downloadName);
        }

        public async Task<(byte[] FileBytes, string ContentType, string DownloadFileName)?> GetVersionFileAsync(long versionId)
        {
            var ver = await _context.ClassCurriculumVersions.FindAsync(versionId);
            if (ver == null) return null;

            var safeStored = Path.GetFileName(ver.StoredFileName);
            var path = Path.Combine(_storageFolder, safeStored);

            if (!File.Exists(path))
            {
                // Nếu file vật lý bị thiếu, tái tạo tức thì từ snapshot ContentJson
                List<CurriculumItemDto> items = new();
                if (!string.IsNullOrWhiteSpace(ver.ContentJson))
                {
                    try { items = JsonSerializer.Deserialize<List<CurriculumItemDto>>(ver.ContentJson) ?? new(); } catch { }
                }
                var bytes = GenerateExcelFileBytes("Lớp", "", "", ver.VersionTag, items);
                await File.WriteAllBytesAsync(path, bytes);
                return (bytes, ver.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ver.FileName);
            }

            var fileData = await File.ReadAllBytesAsync(path);
            return (fileData, ver.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ver.FileName);
        }

        // =========================================================================
        // PRIVATE PARSERS (.xlsx, .docx, .csv, .md, .pdf)
        // =========================================================================

        private List<CurriculumItemDto> ParseExcelStream(Stream stream)
        {
            var items = new List<CurriculumItemDto>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return items;

            var rows = worksheet.RangeUsed()?.RowsUsed()?.ToList();
            if (rows == null || rows.Count < 2) return items;

            // Tìm hàng tiêu đề (header)
            int headerRowIndex = 0;
            int colWeek = 1, colDate = 2, colLiturgical = 3, colCode = 4, colTopic = 5, colKey = 6, colTeacher = 7, colNotes = 8;

            for (int r = 0; r < Math.Min(5, rows.Count); r++)
            {
                var rowCells = rows[r].Cells().Select(c => c.GetString().Trim().ToLowerInvariant()).ToList();
                if (rowCells.Any(c => c.Contains("tuần") || c.Contains("bài") || c.Contains("chủ đề") || c.Contains("ngày")))
                {
                    headerRowIndex = r;
                    for (int c = 1; c <= rows[r].LastCellUsed()?.Address.ColumnNumber; c++)
                    {
                        var headerText = rows[r].Cell(c).GetString().Trim().ToLowerInvariant();
                        if (headerText.Contains("tuần") || headerText == "stt") colWeek = c;
                        else if (headerText.Contains("ngày")) colDate = c;
                        else if (headerText.Contains("phụng vụ") || headerText.Contains("chúa nhật")) colLiturgical = c;
                        else if (headerText.Contains("mã") || headerText.Contains("số bài") || headerText == "bài") colCode = c;
                        else if (headerText.Contains("chủ đề") || headerText.Contains("tiêu đề") || headerText.Contains("bài học") || headerText.Contains("tên")) colTopic = c;
                        else if (headerText.Contains("trọng tâm") || headerText.Contains("ý chính") || headerText.Contains("nội dung")) colKey = c;
                        else if (headerText.Contains("glv") || headerText.Contains("giáo lý viên") || headerText.Contains("phụ trách")) colTeacher = c;
                        else if (headerText.Contains("ghi chú") || headerText.Contains("sự kiện")) colNotes = c;
                    }
                    break;
                }
            }

            int currentWeek = 1;
            for (int r = headerRowIndex + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                var rawWeek = row.Cell(colWeek).GetString().Trim();
                var rawDate = row.Cell(colDate).GetString().Trim();
                var rawTopic = row.Cell(colTopic).GetString().Trim();
                var rawCode = row.Cell(colCode).GetString().Trim();

                if (string.IsNullOrWhiteSpace(rawDate) && string.IsNullOrWhiteSpace(rawTopic) && string.IsNullOrWhiteSpace(rawCode))
                {
                    continue;
                }

                int weekNum = int.TryParse(rawWeek, out var w) ? w : currentWeek;
                var (lessonDate, parsedLiturgical) = ExtractDateAndLiturgical(rawDate);
                var liturgical = !string.IsNullOrWhiteSpace(parsedLiturgical) ? parsedLiturgical : row.Cell(colLiturgical).GetString().Trim();

                var topic = !string.IsNullOrWhiteSpace(rawTopic) ? rawTopic : rawCode;
                var keyPoints = row.Cell(colKey).GetString().Trim();
                var teacher = row.Cell(colTeacher).GetString().Trim();
                var notes = row.Cell(colNotes).GetString().Trim();

                var fullText = (topic + " " + rawCode).ToUpperInvariant();
                bool isExam = fullText.Contains("KT") || fullText.Contains("KIỂM TRA") || fullText.Contains("THI");
                bool isOff = fullText.Contains("NGHỈ") || fullText.Contains("TẾT") || fullText.Contains("TRUNG THU");

                items.Add(new CurriculumItemDto
                {
                    WeekNumber = weekNum,
                    LessonDate = lessonDate,
                    LessonDateStr = lessonDate?.ToString("dd/MM/yyyy") ?? rawDate,
                    LiturgicalDay = string.IsNullOrWhiteSpace(liturgical) ? "Chúa Nhật" : liturgical,
                    LessonCode = rawCode,
                    TopicTitle = topic,
                    KeyPoints = keyPoints,
                    AssignedTeacherName = teacher,
                    EventNotes = notes,
                    IsExamDay = isExam,
                    IsDayOff = isOff,
                    Semester = weekNum > 18 ? 2 : 1
                });
                currentWeek++;
            }

            return items;
        }

        private List<CurriculumItemDto> ParseWordStream(Stream stream)
        {
            var items = new List<CurriculumItemDto>();
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return items;

            var tables = body.Elements<Table>();
            int currentWeek = 1;

            foreach (var table in tables)
            {
                var rows = table.Elements<TableRow>().Skip(1);
                foreach (var row in rows)
                {
                    var cells = row.Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();
                    if (cells.Count < 2) continue;

                    string rawDate = cells[0];
                    var (lessonDate, liturgical) = ExtractDateAndLiturgical(rawDate);

                    string code = cells.Count > 1 ? cells[1] : "";
                    string topic = cells.Count > 2 ? cells[2] : "";
                    string keyPoints = cells.Count > 3 ? cells[3] : "";
                    string teacher = cells.Count > 4 ? cells[4] : "";
                    string notes = cells.Count > 5 ? cells[5] : "";

                    if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(code))
                    {
                        topic = code;
                    }

                    var fullText = (topic + " " + code).ToUpperInvariant();
                    bool isExam = fullText.Contains("KT") || fullText.Contains("KIỂM TRA") || fullText.Contains("THI");
                    bool isOff = fullText.Contains("NGHỈ") || fullText.Contains("TẾT") || fullText.Contains("TRUNG THU");

                    items.Add(new CurriculumItemDto
                    {
                        WeekNumber = currentWeek++,
                        LessonDate = lessonDate,
                        LessonDateStr = lessonDate?.ToString("dd/MM/yyyy") ?? rawDate,
                        LiturgicalDay = string.IsNullOrWhiteSpace(liturgical) ? "Chúa Nhật" : liturgical,
                        LessonCode = code,
                        TopicTitle = string.IsNullOrWhiteSpace(topic) ? $"Bài học tuần {currentWeek - 1}" : topic,
                        KeyPoints = keyPoints,
                        AssignedTeacherName = teacher,
                        EventNotes = notes,
                        IsExamDay = isExam,
                        IsDayOff = isOff,
                        Semester = currentWeek > 18 ? 2 : 1
                    });
                }
            }

            return items;
        }

        private List<CurriculumItemDto> ParseCsvStream(Stream stream)
        {
            var items = new List<CurriculumItemDto>();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            string? firstLine = reader.ReadLine();
            if (firstLine == null) return items;

            // Phát hiện dấu phân cách (Delimiter detection: comma, semicolon, tab, pipe)
            char delimiter = ',';
            if (firstLine.Contains(';') && firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',')) delimiter = ';';
            else if (firstLine.Contains('\t')) delimiter = '\t';
            else if (firstLine.Contains('|')) delimiter = '|';

            int weekNum = 1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var tokens = SplitCsvLine(line, delimiter);
                if (tokens.Count < 2) continue;

                string rawDate = tokens[0].Trim();
                var (lessonDate, liturgical) = ExtractDateAndLiturgical(rawDate);

                string code = tokens.Count > 1 ? tokens[1].Trim() : "";
                string topic = tokens.Count > 2 ? tokens[2].Trim() : "";
                string keyPoints = tokens.Count > 3 ? tokens[3].Trim() : "";
                string teacher = tokens.Count > 4 ? tokens[4].Trim() : "";
                string notes = tokens.Count > 5 ? tokens[5].Trim() : "";

                if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(code))
                {
                    topic = code;
                }

                if (string.IsNullOrWhiteSpace(rawDate) && string.IsNullOrWhiteSpace(topic)) continue;

                var fullText = (topic + " " + code).ToUpperInvariant();
                bool isExam = fullText.Contains("KT") || fullText.Contains("KIỂM TRA") || fullText.Contains("THI");
                bool isOff = fullText.Contains("NGHỈ") || fullText.Contains("TẾT") || fullText.Contains("TRUNG THU");

                items.Add(new CurriculumItemDto
                {
                    WeekNumber = weekNum++,
                    LessonDate = lessonDate,
                    LessonDateStr = lessonDate?.ToString("dd/MM/yyyy") ?? rawDate,
                    LiturgicalDay = string.IsNullOrWhiteSpace(liturgical) ? "Chúa Nhật" : liturgical,
                    LessonCode = code,
                    TopicTitle = string.IsNullOrWhiteSpace(topic) ? $"Tuần {weekNum - 1}" : topic,
                    KeyPoints = keyPoints,
                    AssignedTeacherName = teacher,
                    EventNotes = notes,
                    IsExamDay = isExam,
                    IsDayOff = isOff,
                    Semester = weekNum > 18 ? 2 : 1
                });
            }

            return items;
        }

        private List<CurriculumItemDto> ParseMarkdownStream(Stream stream)
        {
            var items = new List<CurriculumItemDto>();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            string? line;
            int weekNum = 1;

            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // 1. Dạng Markdown Table: | Tuần | Ngày | Bài | Chủ đề | ... |
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    // Bỏ qua dòng phân cách: |---|---|
                    if (Regex.IsMatch(trimmed, @"^\|[\s\-:]+(\|[\s\-:]+)+\|$")) continue;

                    var parts = trimmed.Split('|', StringSplitOptions.TrimEntries)
                        .Where((s, idx) => idx > 0 && idx < trimmed.Split('|').Length - 1)
                        .ToList();

                    if (parts.Count < 2) continue;

                    // Bỏ qua hàng tiêu đề nếu chứa từ khóa
                    string p0Lower = parts[0].ToLowerInvariant();
                    if (p0Lower.Contains("tuần") || p0Lower.Contains("ngày") || p0Lower.Contains("stt")) continue;

                    string rawDate = parts[0];
                    var (lessonDate, liturgical) = ExtractDateAndLiturgical(rawDate);

                    string code = parts.Count > 1 ? CleanMarkdown(parts[1]) : "";
                    string topic = parts.Count > 2 ? CleanMarkdown(parts[2]) : "";
                    string keyPoints = parts.Count > 3 ? CleanMarkdown(parts[3]) : "";
                    string teacher = parts.Count > 4 ? CleanMarkdown(parts[4]) : "";
                    string notes = parts.Count > 5 ? CleanMarkdown(parts[5]) : "";

                    if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(code)) topic = code;

                    var fullText = (topic + " " + code).ToUpperInvariant();
                    bool isExam = fullText.Contains("KT") || fullText.Contains("KIỂM TRA") || fullText.Contains("THI");
                    bool isOff = fullText.Contains("NGHỈ") || fullText.Contains("TẾT") || fullText.Contains("TRUNG THU");

                    items.Add(new CurriculumItemDto
                    {
                        WeekNumber = weekNum++,
                        LessonDate = lessonDate,
                        LessonDateStr = lessonDate?.ToString("dd/MM/yyyy") ?? rawDate,
                        LiturgicalDay = string.IsNullOrWhiteSpace(liturgical) ? "Chúa Nhật" : liturgical,
                        LessonCode = code,
                        TopicTitle = string.IsNullOrWhiteSpace(topic) ? $"Tuần {weekNum - 1}" : topic,
                        KeyPoints = keyPoints,
                        AssignedTeacherName = teacher,
                        EventNotes = notes,
                        IsExamDay = isExam,
                        IsDayOff = isOff,
                        Semester = weekNum > 18 ? 2 : 1
                    });
                }
                // 2. Dạng Markdown List: - Tuần 1 (07/09/2026): Bài 1 - Chủ đề
                else if (trimmed.StartsWith("-") || trimmed.StartsWith("*"))
                {
                    var content = CleanMarkdown(trimmed.TrimStart('-', '*', ' '));
                    var dateMatch = Regex.Match(content, @"(\d{1,2}/\d{1,2}/\d{4})");
                    DateTime? parsedDate = null;
                    if (dateMatch.Success && DateTime.TryParseExact(dateMatch.Groups[1].Value, new[] { "dd/MM/yyyy", "d/M/yyyy" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        parsedDate = dt;
                    }

                    items.Add(new CurriculumItemDto
                    {
                        WeekNumber = weekNum++,
                        LessonDate = parsedDate,
                        LessonDateStr = parsedDate?.ToString("dd/MM/yyyy") ?? "",
                        LiturgicalDay = "Chúa Nhật",
                        TopicTitle = content,
                        Semester = weekNum > 18 ? 2 : 1
                    });
                }
            }

            return items;
        }

        private List<CurriculumItemDto> ParsePdfStream(Stream stream)
        {
            var items = new List<CurriculumItemDto>();
            using var document = PdfDocument.Open(stream);

            int weekNum = 1;
            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"(\d{1,2}/\d{1,2}/\d{4})");
                    if (match.Success)
                    {
                        var rawDate = match.Groups[1].Value;
                        if (DateTime.TryParseExact(rawDate, new[] { "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        {
                            var remaining = line.Replace(rawDate, "").Trim();
                            items.Add(new CurriculumItemDto
                            {
                                WeekNumber = weekNum++,
                                LessonDate = parsedDate,
                                LessonDateStr = rawDate,
                                LiturgicalDay = "Chúa Nhật",
                                TopicTitle = string.IsNullOrWhiteSpace(remaining) ? $"Bài học tuần {weekNum - 1}" : remaining,
                                EventNotes = "Nạp từ file PDF",
                                Semester = weekNum > 18 ? 2 : 1
                            });
                        }
                    }
                }
            }
            return items;
        }

        private static string CleanMarkdown(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input, @"[\*_`~\[\]]", "").Trim();
        }

        private static List<string> SplitCsvLine(string line, char delimiter)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    tokens.Add(sb.ToString().Trim('\"', ' '));
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            tokens.Add(sb.ToString().Trim('\"', ' '));
            return tokens;
        }

        private static (DateTime? Date, string? Liturgical) ExtractDateAndLiturgical(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);

            var dateMatch = Regex.Match(raw, @"(\d{1,2}/\d{1,2}/\d{4})");
            if (dateMatch.Success && DateTime.TryParseExact(dateMatch.Groups[1].Value, new[] { "dd/MM/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                var liturgical = raw.Replace(dateMatch.Groups[1].Value, "").Trim('(', ')', '-', ' ', '[', ']');
                return (dt, string.IsNullOrWhiteSpace(liturgical) ? null : liturgical);
            }
            return (null, raw);
        }

        // =========================================================================
        // EXCEL GENERATION (.xlsx) THEO VERSION NGÀY SỬA (TASK-P5-07.3)
        // =========================================================================

        private static byte[] GenerateExcelFileBytes(string className, string gradeLevel, string academicYearName, string versionTag, List<CurriculumItemDto> items)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Kế Hoạch Bài Giảng");

            // Màu chủ đạo Giáo xứ
            var primaryColor = XLColor.FromArgb(27, 77, 62); // #1B4D3E
            var secondaryColor = XLColor.FromArgb(240, 248, 255); // Nhạt

            // 1. Banner Tiêu đề
            ws.Cell(1, 1).Value = "GIÁO XỨ PHÚ TRUNG - XỨ ĐOÀN THIẾU NHI THÁNH THỂ KITÔ VUA";
            ws.Range(1, 1, 1, 9).Merge().Style
                .Font.SetBold(true)
                .Font.SetFontSize(11)
                .Font.SetFontColor(XLColor.DarkGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = "KẾ HOẠCH BÀI GIẢNG & PHÂN PHỐI CHƯƠNG TRÌNH NĂM HỌC";
            ws.Range(2, 1, 2, 9).Merge().Style
                .Font.SetBold(true)
                .Font.SetFontSize(16)
                .Font.SetFontColor(primaryColor)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            string classSubtitle = $"Lớp: {className} ({gradeLevel}) | Niên khóa: {academicYearName} | Phiên bản: {versionTag} | Ngày xuất: {DateTime.UtcNow:dd/MM/yyyy}";
            ws.Cell(3, 1).Value = classSubtitle;
            ws.Range(3, 1, 3, 9).Merge().Style
                .Font.SetItalic(true)
                .Font.SetFontSize(10)
                .Font.SetFontColor(XLColor.DimGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // 2. Header bảng
            int headerRow = 5;
            string[] headers = new[]
            {
                "Tuần", "Ngày Học", "Chúa Nhật / Phụng Vụ", "Số Bài",
                "Tên Chủ Đề / Bài Giảng", "Trọng Tâm Giáo Lý",
                "GLV Phụ Trách", "Phân Loại", "Ghi Chú / Sự Kiện"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.SetBold(true)
                    .Font.SetFontColor(XLColor.White)
                    .Fill.SetBackgroundColor(primaryColor)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            }
            ws.Row(headerRow).Height = 28;

            // 3. Nội dung các dòng tuần học
            int currentRow = headerRow + 1;
            foreach (var item in items.OrderBy(i => i.WeekNumber))
            {
                ws.Cell(currentRow, 1).Value = item.WeekNumber;
                ws.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 2).Value = item.LessonDate?.ToString("dd/MM/yyyy") ?? item.LessonDateStr ?? "";
                ws.Cell(currentRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 3).Value = item.LiturgicalDay ?? "Chúa Nhật";
                ws.Cell(currentRow, 4).Value = item.LessonCode ?? "";
                ws.Cell(currentRow, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 5).Value = item.TopicTitle;
                ws.Cell(currentRow, 6).Value = item.KeyPoints ?? "";
                ws.Cell(currentRow, 7).Value = item.AssignedTeacherName ?? "";

                string tag = "Bình thường";
                if (item.IsExamDay) tag = "KIỂM TRA / THI";
                else if (item.IsDayOff) tag = "NGHỈ LỄ";
                ws.Cell(currentRow, 8).Value = tag;
                ws.Cell(currentRow, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 9).Value = item.EventNotes ?? "";

                // Tô màu xen kẽ và highlight ngày thi / nghỉ
                if (item.IsExamDay)
                {
                    ws.Range(currentRow, 1, currentRow, 9).Style.Fill.SetBackgroundColor(XLColor.FromArgb(255, 235, 238)); // Đỏ nhạt
                    ws.Cell(currentRow, 8).Style.Font.SetBold(true).Font.SetFontColor(XLColor.Red);
                }
                else if (item.IsDayOff)
                {
                    ws.Range(currentRow, 1, currentRow, 9).Style.Fill.SetBackgroundColor(XLColor.FromArgb(255, 248, 225)); // Vàng nhạt
                    ws.Cell(currentRow, 8).Style.Font.SetBold(true).Font.SetFontColor(XLColor.DarkOrange);
                }
                else if (currentRow % 2 == 0)
                {
                    ws.Range(currentRow, 1, currentRow, 9).Style.Fill.SetBackgroundColor(secondaryColor);
                }

                ws.Row(currentRow).Height = 22;
                currentRow++;
            }

            // Đóng khung viền
            var tableRange = ws.Range(headerRow, 1, Math.Max(headerRow, currentRow - 1), 9);
            tableRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Medium);

            // Auto-fit độ rộng cột
            ws.Columns(1, 9).AdjustToContents();
            ws.Column(1).Width = 8;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 35;
            ws.Column(6).Width = 35;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 18;
            ws.Column(9).Width = 25;

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
