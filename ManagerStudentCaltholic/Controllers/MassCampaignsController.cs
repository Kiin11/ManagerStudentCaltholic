using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class MassCampaignsController : Controller
    {
        private readonly IMassCampaignService _campaignService;

        public MassCampaignsController(IMassCampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? classId = null, long? campaignId = null)
        {
            var vm = await _campaignService.BuildDashboardViewModelAsync(User, classId, campaignId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMassCampaignRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu thiết lập chiến dịch chưa hợp lệ. Vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Index), new { classId = dto.ClassRoomId });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message, campaign) = await _campaignService.CreateCampaignAsync(dto, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
                return RedirectToAction(nameof(Index), new { classId = dto.ClassRoomId });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index), new { classId = dto.ClassRoomId, campaignId = campaign?.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateConfig(UpdateMassCampaignConfigDto dto, int classRoomId)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message) = await _campaignService.UpdateCampaignConfigAsync(dto, User, ip);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Index), new { classId = classRoomId, campaignId = dto.CampaignId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCell([FromBody] UpdateCampaignCellRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu ô điểm danh không hợp lệ." });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message, updatedRow) = await _campaignService.UpdateSingleCellAsync(dto, User, ip);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new
            {
                success = true,
                message,
                rowStats = updatedRow == null ? null : new
                {
                    enrollmentId = updatedRow.EnrollmentId,
                    presentDays = updatedRow.PresentDays,
                    lateDays = updatedRow.LateDays,
                    excusedDays = updatedRow.ExcusedDays,
                    absentDays = updatedRow.AbsentDays,
                    totalAttendedDays = updatedRow.TotalAttendedDays,
                    completionPercentage = updatedRow.CompletionPercentage,
                    currentStreak = updatedRow.CurrentStreak,
                    maxStreak = updatedRow.MaxStreak,
                    isTargetMet = updatedRow.IsTargetMet
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkMarkDay([FromBody] BulkMarkCampaignDayRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu điểm danh ngày không hợp lệ." });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message, updatedCount) = await _campaignService.BulkMarkDayAsync(dto, User, ip);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, updatedCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanCampaignQr([FromBody] CampaignQrScanRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Thiếu mã thiếu nhi hoặc mã chiến dịch." });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message, studentInfo) = await _campaignService.RecordQrScanAsync(dto, User, ip);

            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, data = studentInfo });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(long campaignId)
        {
            try
            {
                var bytes = await _campaignService.ExportCampaignMatrixToExcelAsync(campaignId, User);
                var fileName = $"ChienDichDiLe_{campaignId}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Không tìm thấy chiến dịch đi lễ yêu cầu.");
            }
        }
    }
}