using Hangfire;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class BackgroundJobsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ICacheService _cache;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BackgroundJobsController> _logger;

        public BackgroundJobsController(
            ParishDbContext context,
            IBackgroundJobClient backgroundJobClient,
            ICacheService cache,
            IWebHostEnvironment env,
            ILogger<BackgroundJobsController> logger)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
            _cache = cache;
            _env = env;
            _logger = logger;
        }

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// 1. POST: /BackgroundJobs/EnqueueParishReport (Đặt lịch chạy ngầm xuất Excel toàn xứ / theo khối - TASK-P3-10.2)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> EnqueueParishReport([FromForm] int academicYearId, [FromForm] string? gradeLevel)
        {
            string targetGrade = string.IsNullOrWhiteSpace(gradeLevel) ? "ALL" : gradeLevel.Trim();

            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return Forbid();
                }
                targetGrade = managedGrade;
            }

            string jobTrackerId = Guid.NewGuid().ToString("N");
            string operatorName = User.Identity?.Name ?? "Staff";
            string cacheKey = BackgroundExportJobService.GetJobCacheKey(jobTrackerId);

            await _cache.SetAsync(cacheKey, new BackgroundExportMetaDto
            {
                JobId = jobTrackerId,
                Status = "PROCESSING",
                Title = $"Báo cáo Giáo lý ({(targetGrade == "ALL" ? "Toàn Giáo Xứ" : targetGrade)})",
                RequestedBy = operatorName,
                CreatedAtUtc = DateTime.UtcNow
            }, TimeSpan.FromHours(24));

            _backgroundJobClient.Enqueue<IBackgroundExportJobService>(
                svc => svc.GenerateParishFullExcelReportJobAsync(jobTrackerId, academicYearId, targetGrade, operatorName));

            return Json(new
            {
                success = true,
                jobId = jobTrackerId,
                message = "Đã đưa yêu cầu xuất báo cáo Excel vào hàng đợi chạy nền! Bạn sẽ nhận được thông báo tải xuống ngay khi hoàn tất."
            });
        }

        /// <summary>
        /// 2. POST: /BackgroundJobs/EnqueueClassReport (Đặt lịch chạy ngầm xuất danh sách lớp)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnqueueClassReport([FromForm] int classId)
        {
            var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
            if (targetClass == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lớp học." });
            }

            if (!IsLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (string.IsNullOrWhiteSpace(managedGrade) ||
                        !string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid);
                    bool assigned = await _context.ClassTeachers.AnyAsync(ct => ct.ClassRoomId == classId && ct.UserId == uid);
                    if (!assigned) return Forbid();
                }
            }

            string jobTrackerId = Guid.NewGuid().ToString("N");
            string operatorName = User.Identity?.Name ?? "Staff";
            string cacheKey = BackgroundExportJobService.GetJobCacheKey(jobTrackerId);

            await _cache.SetAsync(cacheKey, new BackgroundExportMetaDto
            {
                JobId = jobTrackerId,
                Status = "PROCESSING",
                Title = $"Danh sách lớp {targetClass.Name}",
                RequestedBy = operatorName,
                CreatedAtUtc = DateTime.UtcNow
            }, TimeSpan.FromHours(24));

            _backgroundJobClient.Enqueue<IBackgroundExportJobService>(
                svc => svc.GenerateClassExcelReportJobAsync(jobTrackerId, classId, operatorName));

            return Json(new
            {
                success = true,
                jobId = jobTrackerId,
                message = $"Đang tạo file Excel cho lớp {targetClass.Name} trong nền..."
            });
        }

        /// <summary>
        /// 3. GET: /BackgroundJobs/GetJobStatus?jobId=... (API Polling kiểm tra tiến độ hoàn thành - TASK-P3-10.4)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { success = false, message = "Thiếu mã công việc." });
            }

            var meta = await _cache.GetAsync<BackgroundExportMetaDto>(BackgroundExportJobService.GetJobCacheKey(jobId.Trim()));
            if (meta == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy thông tin công việc hoặc đã hết hạn." });
            }

            return Json(new
            {
                success = true,
                jobId = meta.JobId,
                status = meta.Status,
                title = meta.Title,
                errorMessage = meta.ErrorMessage,
                downloadUrl = meta.Status == "COMPLETED"
                    ? Url.Action(nameof(DownloadExport), "BackgroundJobs", new { jobId = meta.JobId })
                    : null
            });
        }

        /// <summary>
        /// 4. GET: /BackgroundJobs/DownloadExport?jobId=... (Tải xuống file báo cáo từ PrivateUploads/exports an toàn)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadExport(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return BadRequest();

            var meta = await _cache.GetAsync<BackgroundExportMetaDto>(BackgroundExportJobService.GetJobCacheKey(jobId.Trim()));
            if (meta == null || meta.Status != "COMPLETED" || string.IsNullOrWhiteSpace(meta.FileName))
            {
                return NotFound("Báo cáo chưa hoàn tất hoặc đã hết hạn lưu trữ.");
            }

            // Chặn Path Traversal tuyệt đối bằng Path.GetFileName
            var safeFileName = Path.GetFileName(meta.FileName);
            var exportsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports"));
            var fullFilePath = Path.GetFullPath(Path.Combine(exportsRoot, safeFileName));

            if (!fullFilePath.StartsWith(exportsRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullFilePath))
            {
                return NotFound("Không tìm thấy tệp báo cáo trên máy chủ.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullFilePath);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", safeFileName);
        }
    }
}