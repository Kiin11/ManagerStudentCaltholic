using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class AnnouncementsController : Controller
    {
        private const int MaxAnnouncementAttachments = 5;

        private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".xlsx", ".xls"
        };

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly ParishDbContext _context;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly IImageOptimizationService _imageOptimizationService;
        private readonly GoogleDriveOptions _driveOptions;
        private readonly ILogger<AnnouncementsController> _logger;

        public AnnouncementsController(
            ParishDbContext context,
            IGoogleDriveStorageService storageService,
            IImageOptimizationService imageOptimizationService,
            IOptions<GoogleDriveOptions> driveOptions,
            ILogger<AnnouncementsController> logger)
        {
            _context = context;
            _storageService = storageService;
            _imageOptimizationService = imageOptimizationService;
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
        /// Lấy danh sách các lớp mà người dùng hiện tại được phép đăng/quản lý bảng tin
        /// </summary>
        private async Task<List<ClassRoom>> GetAuthorizedClassesAsync(int currentYearId)
        {
            var query = _context.Classes.Where(c => currentYearId == 0 || c.AcademicYearId == currentYearId);

            if (IsLeadership)
            {
                return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
            }

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    query = query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
                else
                {
                    query = query.Where(c => c.GradeLevel == managedGrade || c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
            }
            else
            {
                query = query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
            }

            return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Kiểm tra người dùng có quyền ghim hoặc xóa một bài thông báo cụ thể hay không
        /// </summary>
        private async Task<bool> CanManageAnnouncementAsync(Announcement item, int currentYearId)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade))
                {
                    if (item.Scope == "GRADE" && string.Equals(item.TargetGradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (item.Scope == "CLASS" && item.TargetClassRoomId.HasValue)
                    {
                        var cls = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.TargetClassRoomId.Value);
                        if (cls != null && string.Equals(cls.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            if (item.Scope == "CLASS" && item.TargetClassRoomId.HasValue)
            {
                return await _context.ClassTeachers.AnyAsync(ct =>
                    ct.ClassRoomId == item.TargetClassRoomId.Value &&
                    ct.UserId == CurrentUserId &&
                    (currentYearId == 0 || ct.AcademicYearId == currentYearId));
            }

            return false;
        }


        /// <summary>
        /// 1. GET: /Announcements (Xem & Lọc Bảng Tin Toàn Xứ / Khối / Lớp - TASK-P3-07.1)
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="gradeLevel"></param>
        /// <param name="classId"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? scope, string? gradeLevel, int? classId, string? search)
        {
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);
            var authorizedClassIds = authorizedClasses.Select(c => c.Id).ToHashSet();
            var authorizedGrades = authorizedClasses.Select(c => c.GradeLevel).Distinct().ToHashSet();

            var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
            if (!string.IsNullOrWhiteSpace(managedGrade))
            {
                authorizedGrades.Add(managedGrade);
            }

            // P2-SEC-04 FIX: Nếu tự truyền classId của lớp không thuộc quyền phụ trách -> Chặn 403
            if (!IsLeadership && classId.HasValue && classId.Value > 0 && !authorizedClassIds.Contains(classId.Value))
            {
                return Forbid();
            }

            var nowUtc = DateTime.UtcNow;
            var query = _context.Announcements
                .Include(a => a.TargetClassRoom)
                .Where(a => a.IsActive && (!a.ExpireAt.HasValue || a.ExpireAt.Value >= nowUtc));

            // P2-SEC-04 FIX: Giới chặt phạm vi xem đối với Trưởng khối & Giáo lý viên
            if (!IsLeadership)
            {
                query = query.Where(a =>
                    a.Scope == "ALL" ||
                    a.Scope == "PARISH" ||
                    (a.Scope == "GRADE" && a.TargetGradeLevel != null && authorizedGrades.Contains(a.TargetGradeLevel)) ||
                    (a.Scope == "CLASS" && a.TargetClassRoomId.HasValue && authorizedClassIds.Contains(a.TargetClassRoomId.Value)));
            }

            if (!string.IsNullOrEmpty(scope) && scope != "ALL")
            {
                if (scope == "PARISH")
                {
                    query = query.Where(a => a.Scope == "ALL" || a.Scope == "PARISH");
                }
                else
                {
                    query = query.Where(a => a.Scope == scope);
                }
            }

            if (!string.IsNullOrEmpty(gradeLevel) && gradeLevel != "ALL")
            {
                query = query.Where(a => a.TargetGradeLevel == gradeLevel ||
                                         (a.TargetClassRoom != null && a.TargetClassRoom.GradeLevel == gradeLevel));
            }

            if (classId.HasValue && classId.Value > 0)
            {
                query = query.Where(a => a.TargetClassRoomId == classId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(a => a.Title.Contains(kw) || a.Content.Contains(kw));
            }

            var announcements = await query
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var announcementIds = announcements.Select(a => a.Id).ToList();
            ViewBag.AnnouncementAttachments = announcementIds.Count > 0
                ? await _storageService.GetMediaMapForEntitiesAsync(
                    nameof(Announcement),
                    announcementIds,
                    CloudMediaCategory.AnnouncementDoc)
                : new Dictionary<long, List<CloudMediaSummaryDto>>();

            ViewBag.Classes = new SelectList(authorizedClasses, "Id", "Name", classId);
            ViewBag.AuthorizedClassIds = authorizedClassIds;
            ViewBag.CanPostAll = IsLeadership;
            ViewBag.CanPostGrade = IsLeadership || User.IsInRole(UserRole.BranchHead);
            ViewBag.CanPostClass = IsLeadership || authorizedClasses.Any();
            ViewBag.ManagedGradeLevel = managedGrade;

            ViewBag.SelectedScope = scope ?? "ALL";
            ViewBag.SelectedGrade = gradeLevel ?? "ALL";
            ViewBag.SelectedClassId = classId;
            ViewBag.SearchKeyword = search;

            return View(announcements);
        }

        /// <summary>
        /// 2. POST: /Announcements/Create (Hỗ trợ BĐH đăng Toàn xứ, Trưởng khối đăng theo Khối, GLV đăng dặn dò theo Lớp - TASK-P3-07.1)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Create(
            [Bind("Title,Content,Scope,TargetGradeLevel,TargetClassRoomId,Priority,IsPinned,ExpireAt")] Announcement model,
            List<IFormFile>? attachments)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Content))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung thông báo.";
                return RedirectToAction(nameof(Index));
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;
            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);

            // Kiểm tra và ép phạm vi đăng bài (Scope) theo đúng vai trò
            if (IsLeadership)
            {
                model.Scope = string.IsNullOrWhiteSpace(model.Scope) ? "ALL" : model.Scope.Trim().ToUpperInvariant();
            }
            else if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (model.Scope == "GRADE")
                {
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        TempData["ErrorMessage"] = "Tài khoản Trưởng khối chưa được gán khối phụ trách.";
                        return RedirectToAction(nameof(Index));
                    }
                    model.TargetGradeLevel = managedGrade;
                    model.TargetClassRoomId = null;
                }
                else
                {
                    model.Scope = "CLASS";
                    if (!model.TargetClassRoomId.HasValue || !authorizedClasses.Any(c => c.Id == model.TargetClassRoomId.Value))
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ được đăng thông báo cho các lớp thuộc khối mình phụ trách.";
                        return RedirectToAction(nameof(Index));
                    }
                    var targetCls = authorizedClasses.First(c => c.Id == model.TargetClassRoomId.Value);
                    model.TargetGradeLevel = targetCls.GradeLevel;
                }
            }
            else
            {
                // Giáo lý viên (Teacher): Chỉ được đăng thông báo dặn dò cho Lớp mình phụ trách (Scope = CLASS)
                model.Scope = "CLASS";
                if (!model.TargetClassRoomId.HasValue || !authorizedClasses.Any(c => c.Id == model.TargetClassRoomId.Value))
                {
                    TempData["ErrorMessage"] = "Bạn chỉ được phép đăng thông báo dặn dò cho lớp học mà mình được phân công phụ trách!";
                    return RedirectToAction(nameof(Index));
                }

                var targetCls = authorizedClasses.First(c => c.Id == model.TargetClassRoomId.Value);
                model.TargetGradeLevel = targetCls.GradeLevel;
            }

            if (model.Scope == "ALL" || model.Scope == "PARISH")
            {
                model.TargetGradeLevel = null;
                model.TargetClassRoomId = null;
            }
            else if (model.Scope == "GRADE")
            {
                model.TargetClassRoomId = null;
            }

            model.Title = model.Title.Trim();
            model.Content = model.Content.Trim();
            model.Priority = string.IsNullOrWhiteSpace(model.Priority) ? "NORMAL" : model.Priority.Trim().ToUpperInvariant();
            model.IsActive = true;
            model.CreatedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Giáo lý viên";
            model.CreatedAt = DateTime.UtcNow;

            if (model.ExpireAt.HasValue)
            {
                model.ExpireAt = DateTime.SpecifyKind(model.ExpireAt.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
            }

            _context.Announcements.Add(model);
            await _context.SaveChangesAsync();

            var currentYearName = !string.IsNullOrWhiteSpace(currentYear?.Name)
                ? currentYear.Name.Trim()
                : DateTime.UtcNow.Year.ToString();

            var (uploadedCount, attachmentWarnings) = await UploadAnnouncementAttachmentsAsync(model, attachments, currentYearName);

            _logger.LogInformation("AUDIT ANNOUNCEMENT_CREATE: User {User} posted '{Title}' (Scope={Scope}, ClassId={ClassId}, Attachments={Attachments})",
                User.Identity?.Name, model.Title, model.Scope, model.TargetClassRoomId, uploadedCount);

            if (attachmentWarnings.Count > 0)
            {
                TempData["WarningMessage"] = string.Join(" ", attachmentWarnings);
            }

            TempData["SuccessMessage"] = uploadedCount > 0
                ? $"Đã đăng bài thông báo / dặn dò thành công (kèm {uploadedCount} tệp đính kèm)!"
                : "Đã đăng bài thông báo / dặn dò thành công!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 3. POST: /Announcements/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            if (!await CanManageAnnouncementAsync(item, currentYear?.Id ?? 0))
            {
                return Forbid();
            }

            var attachments = await _storageService.GetMediaForEntityAsync(
                nameof(Announcement),
                id,
                CloudMediaCategory.AnnouncementDoc);

            _context.Announcements.Remove(item);
            await _context.SaveChangesAsync();

            foreach (var media in attachments)
            {
                try
                {
                    await _storageService.DeleteAsync(media.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Không thể xóa tệp đính kèm CloudMediaId={MediaId} của thông báo Id={AnnouncementId}.",
                        media.Id,
                        id);
                }
            }

            TempData["SuccessMessage"] = "Đã xóa bài thông báo.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<(int UploadedCount, List<string> Warnings)> UploadAnnouncementAttachmentsAsync(
            Announcement model,
            List<IFormFile>? attachments,
            string currentYearName)
        {
            var warnings = new List<string>();
            if (attachments == null || attachments.Count == 0)
            {
                return (0, warnings);
            }

            var submittedFiles = attachments
                .Where(f => f != null && (f.Length > 0 || !string.IsNullOrWhiteSpace(f.FileName)))
                .ToList();

            if (submittedFiles.Count == 0)
            {
                return (0, warnings);
            }

            if (submittedFiles.Count > MaxAnnouncementAttachments)
            {
                warnings.Add($"Chỉ được đính kèm tối đa {MaxAnnouncementAttachments} tệp mỗi thông báo; các tệp vượt quá đã được bỏ qua.");
            }

            var maxBytes = _driveOptions.MaxDocumentBytes > 0
                ? _driveOptions.MaxDocumentBytes
                : 10 * 1024 * 1024;
            var maxMb = maxBytes / (1024 * 1024);
            var folderSegments = new[] { currentYearName, "Announcements" };
            int uploadedCount = 0;

            foreach (var file in submittedFiles.Take(MaxAnnouncementAttachments))
            {
                var safeName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "tep_dinh_kem";
                }

                if (file.Length <= 0)
                {
                    warnings.Add($"Tệp '{safeName}' trống (0 byte) nên đã bị bỏ qua.");
                    continue;
                }

                if (file.Length > maxBytes)
                {
                    warnings.Add($"Tệp '{safeName}' vượt quá dung lượng tối đa {maxMb} MB.");
                    continue;
                }

                var ext = Path.GetExtension(safeName).ToLowerInvariant();
                bool isDoc = AllowedDocumentExtensions.Contains(ext);
                bool isImage = AllowedImageExtensions.Contains(ext);

                if (!isDoc && !isImage)
                {
                    warnings.Add($"Tệp '{safeName}' có định dạng không được hỗ trợ.");
                    continue;
                }

                try
                {
                    Stream? uploadStream = null;
                    string uploadFileName;
                    string uploadContentType;

                    try
                    {
                        if (isDoc)
                        {
                            if (!await HasValidDocumentMagicBytesAsync(file, ext))
                            {
                                warnings.Add($"Tệp '{safeName}' không đúng cấu trúc định dạng {ext}.");
                                continue;
                            }

                            uploadStream = file.OpenReadStream();
                            uploadFileName = safeName;
                            uploadContentType = ResolveDocumentContentType(ext);
                        }
                        else
                        {
                            await using var rawImageStream = file.OpenReadStream();
                            var optimized = await _imageOptimizationService.OptimizeToWebpAsync(
                                rawImageStream,
                                maxWidth: 1600,
                                maxHeight: 1600,
                                quality: 78);

                            uploadStream = optimized.OptimizedStream;
                            uploadFileName = Path.ChangeExtension(safeName, optimized.FileExtension);
                            uploadContentType = optimized.ContentType;
                        }

                        await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = uploadStream,
                            OriginalFileName = uploadFileName,
                            ContentType = uploadContentType,
                            Category = CloudMediaCategory.AnnouncementDoc,
                            EntityName = nameof(Announcement),
                            EntityId = model.Id,
                            FolderSegments = folderSegments,
                            UploadedBy = model.CreatedBy,
                            UploadedByUserId = CurrentUserId > 0 ? CurrentUserId : null,
                            AllowLocalFallback = true
                        });
                        uploadedCount++;
                    }
                    finally
                    {
                        if (uploadStream != null)
                        {
                            await uploadStream.DisposeAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tệp đính kèm '{FileName}' của thông báo Id={AnnouncementId} thất bại khi kiểm tra/tải lên.", safeName, model.Id);
                    warnings.Add($"Không thể đính kèm tệp '{safeName}' ({ex.Message}).");
                }
            }

            return (uploadedCount, warnings);
        }

        private static async Task<bool> HasValidDocumentMagicBytesAsync(IFormFile file, string ext)
        {
            if (file.Length < 4)
            {
                return false;
            }

            byte[] header = new byte[8];
            await using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
            if (bytesRead < 4)
            {
                return false;
            }

            return ext switch
            {
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                ".docx" or ".xlsx" => header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
                ".doc" or ".xls" => bytesRead >= 8
                    && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0
                    && header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1,
                _ => false
            };
        }

        private static string ResolveDocumentContentType(string ext) => ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };

        /// <summary>
        /// 4. POST: /Announcements/TogglePin/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> TogglePin(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            if (!await CanManageAnnouncementAsync(item, currentYear?.Id ?? 0))
            {
                return Forbid();
            }

            item.IsPinned = !item.IsPinned;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = item.IsPinned ? "Đã ghim bài dặn dò lên đầu bảng tin." : "Đã bỏ ghim bài viết.";

            return RedirectToAction(nameof(Index));
        }
    }
}