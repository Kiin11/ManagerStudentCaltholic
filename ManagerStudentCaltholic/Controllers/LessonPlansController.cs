using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class LessonPlansController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IClassCurriculumService _curriculumService;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly GoogleDriveOptions _driveOptions;
        private readonly ILogger<LessonPlansController> _logger;

        public LessonPlansController(
            ParishDbContext context,
            IClassCurriculumService curriculumService,
            IGoogleDriveStorageService storageService,
            IOptions<GoogleDriveOptions> driveOptions,
            ILogger<LessonPlansController> logger)
        {
            _context = context;
            _curriculumService = curriculumService;
            _storageService = storageService;
            _driveOptions = driveOptions.Value;
            _logger = logger;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// SEC-NEW-05 FIX: Kiểm tra người dùng hiện tại có thẩm quyền quản lý tài liệu của lớp hay không
        /// </summary>
        private async Task<bool> CanManageClassLessonAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id && ct.UserId == CurrentUserId);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? classRoomId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == yearId);

            // Lọc danh sách lớp theo đúng phạm vi quyền hạn
            if (!IsLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    classesQuery = !string.IsNullOrWhiteSpace(managedGrade)
                        ? classesQuery.Where(c => c.GradeLevel == managedGrade || c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId))
                        : classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
                else
                {
                    classesQuery = classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
            }

            var classes = await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (classRoomId.HasValue && !classes.Any(c => c.Id == classRoomId.Value))
            {
                return Forbid();
            }

            var selectedClassId = classRoomId ?? classes.FirstOrDefault()?.Id ?? 0;
            var selectedClass = classes.FirstOrDefault(c => c.Id == selectedClassId);

            bool canManage = selectedClass != null && await CanManageClassLessonAsync(selectedClass);

            // 1. Lấy dữ liệu Kế hoạch lớp (FEAT-P5-07)
            ClassCurriculumDocumentDto? curriculumDoc = null;
            List<ClassCurriculumVersionDto> versions = new();

            if (selectedClassId > 0)
            {
                curriculumDoc = await _curriculumService.GetCurriculumByClassAsync(selectedClassId, yearId);
                if (curriculumDoc != null && curriculumDoc.Id > 0)
                {
                    versions = await _curriculumService.GetVersionsAsync(curriculumDoc.Id);
                }
            }

            // 2. Lấy danh sách tài liệu đính kèm truyền thống (kèm CloudFile để hiển thị trạng thái đồng bộ Drive)
            var legacyDocs = await _context.ClassLessonDocuments
                .Include(d => d.CloudFile)
                .Where(d => d.ClassRoomId == selectedClassId && d.AcademicYearId == yearId)
                .OrderByDescending(d => d.UploadedAt)
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new LessonPlanWorkspaceViewModel
            {
                Classes = classes,
                CurrentYear = currentYear,
                SelectedClassId = selectedClassId,
                SelectedClassName = selectedClass?.Name ?? "Chưa chọn lớp",
                SelectedGrade = selectedClass?.GradeLevel ?? "",
                CanManage = canManage,
                CurriculumDocument = curriculumDoc,
                Versions = versions,
                LegacyDocuments = legacyDocs
            };

            ViewBag.Classes = classes;
            ViewBag.CurrentYear = currentYear;
            ViewBag.SelectedClassId = selectedClassId;
            ViewBag.SelectedClassName = viewModel.SelectedClassName;
            ViewBag.SelectedGrade = viewModel.SelectedGrade;

            return View(viewModel);
        }

        // =========================================================================
        // FEAT-P5-07: API BIÊN TẬP, NẠP FILE, XUẤT VERSION & KHÔI PHỤC (TASK-P5-07.2, 07.3, 07.4)
        // =========================================================================

        /// <summary>
        /// TASK-P5-07.2: Đọc file kế hoạch lớp (.xlsx, .docx, .csv, .md, .pdf) và nạp lên bảng phân phối
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> ParseCurriculumFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file kế hoạch để nạp." });
            }

            const long maxFileSize = 15 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return Json(new { success = false, message = "Dung lượng file vượt quá giới hạn 15MB." });
            }

            var result = await _curriculumService.ParseUploadedCurriculumFileAsync(file);
            return Json(result);
        }

        /// <summary>
        /// TASK-P5-07.3: Lưu kế hoạch trực tuyến, tự động sinh file đính kèm gắn version & ngày sửa, đồng bộ ClassLessonPlans
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> SaveCurriculum([FromBody] SaveCurriculumRequestDto request)
        {
            if (request == null || request.ClassRoomId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu kế hoạch lớp không hợp lệ." });
            }

            var targetClass = await _context.Classes.FindAsync(request.ClassRoomId);
            if (targetClass == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
            }

            if (!await CanManageClassLessonAsync(targetClass))
            {
                return Json(new { success = false, message = "Bạn không có thẩm quyền chỉnh sửa kế hoạch lớp học này!" });
            }

            var userName = User.Identity?.Name ?? "GLV";
            var result = await _curriculumService.SaveCurriculumAsync(request, CurrentUserId, userName);

            return Json(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data
            });
        }

        /// <summary>
        /// TASK-P5-07.4: Lấy chi tiết nội dung tuần học của một phiên bản cũ
        /// </summary>
        [HttpGet("LessonPlans/GetVersionDetail/{versionId:long}")]
        public async Task<IActionResult> GetVersionDetail(long versionId)
        {
            var ver = await _context.ClassCurriculumVersions
                .Include(v => v.CurriculumDocument)
                .ThenInclude(d => d.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId);

            if (ver == null) return NotFound(new { success = false, message = "Không tìm thấy phiên bản." });

            if (ver.CurriculumDocument?.ClassRoom != null && !await CanManageClassLessonAsync(ver.CurriculumDocument.ClassRoom))
            {
                return Forbid();
            }

            var detail = await _curriculumService.GetVersionDetailAsync(versionId);
            return Json(new { success = true, data = detail });
        }

        /// <summary>
        /// TASK-P5-07.4: Tải file đính kèm được tự động sinh của phiên bản ngày sửa
        /// </summary>
        [HttpGet("LessonPlans/DownloadCurriculumVersion/{versionId:long}")]
        public async Task<IActionResult> DownloadCurriculumVersion(long versionId)
        {
            var ver = await _context.ClassCurriculumVersions
                .Include(v => v.CurriculumDocument)
                .ThenInclude(d => d.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId);

            if (ver == null) return NotFound("Không tìm thấy phiên bản yêu cầu.");

            if (ver.CurriculumDocument?.ClassRoom != null && !await CanManageClassLessonAsync(ver.CurriculumDocument.ClassRoom))
            {
                return Forbid();
            }

            var fileResult = await _curriculumService.GetVersionFileAsync(versionId);
            if (fileResult == null) return NotFound("Tệp đính kèm không tồn tại.");

            return File(fileResult.Value.FileBytes, fileResult.Value.ContentType, fileResult.Value.DownloadFileName);
        }

        /// <summary>
        /// TASK-P5-07.4: Xuất file Excel bảng kế hoạch lớp hiện tại
        /// </summary>
        [HttpGet("LessonPlans/ExportCurrentCurriculumExcel")]
        public async Task<IActionResult> ExportCurrentCurriculumExcel(long documentId, [FromQuery] long? versionId = null)
        {
            var doc = await _context.ClassCurriculumDocuments
                .Include(d => d.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null || doc.ClassRoom == null) return NotFound("Không tìm thấy tài liệu kế hoạch.");

            if (!await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Forbid();
            }

            var fileResult = await _curriculumService.ExportCurriculumToExcelAsync(documentId, versionId);
            return File(fileResult.FileBytes, fileResult.ContentType, fileResult.DownloadFileName);
        }

        /// <summary>
        /// TASK-P5-07.4: Khôi phục (Rollback) nội dung kế hoạch lớp về phiên bản ngày sửa cũ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> RollbackCurriculum([FromForm] long documentId, [FromForm] long versionId)
        {
            var doc = await _context.ClassCurriculumDocuments
                .Include(d => d.ClassRoom)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null || doc.ClassRoom == null)
            {
                return Json(new { success = false, message = "Không tìm thấy kế hoạch lớp." });
            }

            if (!await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Json(new { success = false, message = "Bạn không có quyền khôi phục kế hoạch của lớp này!" });
            }

            var userName = User.Identity?.Name ?? "GLV";
            var result = await _curriculumService.RollbackCurriculumAsync(documentId, versionId, CurrentUserId, userName);

            return Json(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data
            });
        }

        // =========================================================================
        // LEGACY DOCUMENT MANAGEMENT (ClassLessonDocuments)
        // =========================================================================

        private static async Task<bool> HasValidMagicBytesAsync(IFormFile file, string ext)
        {
            if (file.Length < 4) return false;

            byte[] header = new byte[8];
            await using var stream = file.OpenReadStream();
            int bytesRead = 0;
            while (bytesRead < header.Length)
            {
                int read = await stream.ReadAsync(header.AsMemory(bytesRead, header.Length - bytesRead));
                if (read == 0) break;
                bytesRead += read;
            }
            if (bytesRead < 4) return false;

            return ext switch
            {
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                ".docx" or ".xlsx" => header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
                ".doc" or ".xls" => bytesRead >= 8 &&
                                    header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&
                                    header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1,
                _ => false
            };
        }

        private static string ResolveLessonDocumentContentType(string ext, string? rawContentType)
        {
            if (!string.IsNullOrWhiteSpace(rawContentType) && rawContentType.Trim().Length <= 100)
            {
                return rawContentType.Trim();
            }

            return ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                _ => "application/octet-stream"
            };
        }

        private static string TruncateValue(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];

        private IActionResult FileStreamResponse(Stream stream, string contentType, string fileName, bool inline)
        {
            if (inline && string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(fileName)}\"";
                return File(stream, contentType);
            }

            return File(stream, contentType, fileName);
        }

        private static void DeleteLegacyFileIfContained(string rootDirectory, string safeStoredName)
        {
            if (string.IsNullOrWhiteSpace(safeStoredName)) return;

            var fullRoot = Path.GetFullPath(rootDirectory);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            var physicalPath = Path.GetFullPath(Path.Combine(fullRoot, safeStoredName));
            if (physicalPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(physicalPath))
            {
                try { System.IO.File.Delete(physicalPath); } catch { }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] int classRoomId, [FromForm] string? description)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file kế hoạch để tải lên." });
                }

                var maxFileSize = _driveOptions.MaxLessonPlanBytes > 0
                    ? _driveOptions.MaxLessonPlanBytes
                    : 25L * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    var maxMb = Math.Round(maxFileSize / (1024d * 1024d), 1);
                    return Json(new { success = false, message = $"Dung lượng file vượt quá giới hạn {maxMb:0.#} MB." });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls" };
                if (!allowedExtensions.Contains(ext) || !await HasValidMagicBytesAsync(file, ext))
                {
                    return Json(new { success = false, message = "Định dạng hoặc cấu trúc tệp không hợp lệ! Chỉ chấp nhận tệp chuẩn .pdf, .docx, .doc, .xlsx, .xls." });
                }

                var targetClass = await _context.Classes
                    .Include(c => c.AcademicYear)
                    .FirstOrDefaultAsync(c => c.Id == classRoomId);
                if (targetClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
                }

                if (!await CanManageClassLessonAsync(targetClass))
                {
                    return Json(new { success = false, message = "Bạn không có thẩm quyền tải giáo án lên lớp học này!" });
                }

                var safeOriginalName = TruncateValue(Path.GetFileName(file.FileName), 255);
                if (string.IsNullOrWhiteSpace(safeOriginalName))
                {
                    safeOriginalName = $"lesson_plan{ext}";
                }

                var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var safeContentType = ResolveLessonDocumentContentType(ext, file.ContentType);
                var safeDescription = TruncateValue(
                    string.IsNullOrWhiteSpace(description) ? safeOriginalName : description.Trim(),
                    500);
                var safeUploadedBy = TruncateValue(User.Identity?.Name ?? "GLV", 100);
                var yearName = !string.IsNullOrWhiteSpace(targetClass.AcademicYear?.Name)
                    ? targetClass.AcademicYear.Name
                    : $"Year_{targetClass.AcademicYearId}";

                var strategy = _context.Database.CreateExecutionStrategy();
                var savedDoc = await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var doc = new ClassLessonDocument
                        {
                            ClassRoomId = targetClass.Id,
                            AcademicYearId = targetClass.AcademicYearId,
                            FileName = safeOriginalName,
                            StoredFileName = storedFileName,
                            FilePath = "/LessonPlans/DownloadDocument/0",
                            ContentType = safeContentType,
                            FileSize = file.Length,
                            Description = safeDescription,
                            UploadedBy = safeUploadedBy,
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.ClassLessonDocuments.Add(doc);
                        await _context.SaveChangesAsync();

                        await using var uploadStream = file.OpenReadStream();
                        var media = await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = uploadStream,
                            OriginalFileName = safeOriginalName,
                            ContentType = safeContentType,
                            Category = CloudMediaCategory.LessonPlan,
                            EntityName = nameof(ClassLessonDocument),
                            EntityId = doc.Id,
                            FolderSegments = new[] { yearName, "LessonPlans" },
                            UploadedBy = User.Identity?.Name,
                            UploadedByUserId = CurrentUserId > 0 ? CurrentUserId : null
                        });

                        doc.CloudFileId = media.Id;
                        doc.StoredFileName = TruncateValue(media.StoredFileName, 255);
                        doc.FilePath = $"/LessonPlans/DownloadDocument/{doc.Id}";
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();
                        return doc;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                _logger.LogInformation("Người dùng {User} đã upload tài liệu {FileName} (DocId={DocId}, CloudFileId={CloudFileId}) cho lớp {ClassId}",
                    User.Identity?.Name, safeOriginalName, savedDoc.Id, savedDoc.CloudFileId, classRoomId);

                return Json(new { success = true, message = $"Đã tải lên và lưu file '{safeOriginalName}' an toàn!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload tài liệu giáo án cho lớp {ClassId}", classRoomId);
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi lưu tệp." });
            }
        }

        [HttpGet("LessonPlans/DownloadDocument/{id:long}")]
        public async Task<IActionResult> DownloadDocument(long id, [FromQuery] bool inline = true)
        {
            var doc = await _context.ClassLessonDocuments
                .Include(d => d.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null || doc.ClassRoom == null) return NotFound();

            if (!await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Forbid();
            }

            var isPdf = string.Equals(Path.GetExtension(doc.FileName), ".pdf", StringComparison.OrdinalIgnoreCase);

            if (doc.CloudFileId.HasValue)
            {
                var cloudResult = await _storageService.DownloadAsync(doc.CloudFileId.Value);
                if (cloudResult != null)
                {
                    var cloudContentType = isPdf
                        ? "application/pdf"
                        : (!string.IsNullOrWhiteSpace(cloudResult.ContentType)
                            ? cloudResult.ContentType
                            : (string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType));
                    var downloadName = string.IsNullOrWhiteSpace(doc.FileName) ? cloudResult.FileName : doc.FileName;
                    return FileStreamResponse(cloudResult.ContentStream, cloudContentType, downloadName, inline);
                }
            }

            var safeStoredName = Path.GetFileName(doc.StoredFileName);
            string? targetPath = null;
            if (!string.IsNullOrWhiteSpace(safeStoredName))
            {
                var privatePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads", "lesson_plans", safeStoredName));
                var legacyPublicPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lesson_plans", safeStoredName));

                targetPath = System.IO.File.Exists(privatePath)
                    ? privatePath
                    : (System.IO.File.Exists(legacyPublicPath) ? legacyPublicPath : null);
            }

            if (targetPath == null) return NotFound("Không tìm thấy tệp giáo án trên máy chủ.");

            var contentType = (isPdf || string.Equals(Path.GetExtension(safeStoredName), ".pdf", StringComparison.OrdinalIgnoreCase))
                ? "application/pdf"
                : (string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType);

            var fileStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return FileStreamResponse(fileStream, contentType, doc.FileName, inline);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> DeleteDocument([FromForm] long documentId)
        {
            var doc = await _context.ClassLessonDocuments
                .Include(d => d.ClassRoom)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) return Json(new { success = false, message = "Không tìm thấy tài liệu cần xóa." });

            if (doc.ClassRoom != null && !await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa tài liệu của khối/lớp khác!" });
            }

            var cloudFileId = doc.CloudFileId;
            var safeStoredName = Path.GetFileName(doc.StoredFileName);

            _context.ClassLessonDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            if (cloudFileId.HasValue)
            {
                await _storageService.DeleteAsync(cloudFileId.Value);
            }

            if (!string.IsNullOrWhiteSpace(safeStoredName))
            {
                DeleteLegacyFileIfContained(Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads", "lesson_plans"), safeStoredName);
                DeleteLegacyFileIfContained(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lesson_plans"), safeStoredName);
            }

            return Json(new { success = true, message = "Đã xóa tài liệu kế hoạch thành công!" });
        }
    }
}
