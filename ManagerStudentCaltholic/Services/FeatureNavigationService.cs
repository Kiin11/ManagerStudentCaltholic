using System.Globalization;
using System.Security.Claims;
using System.Text;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class FeatureNavigationService : IFeatureNavigationService
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<FeatureNavigationService> _logger;

        public FeatureNavigationService(
            ParishDbContext context,
            ILogger<FeatureNavigationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task EnsureDefaultRulesSeededAsync()
        {
            var existingRules = await _context.FeaturePermissionRules.ToListAsync();
            var existingMap = existingRules.ToDictionary(r => r.FeatureCode);

            var defaults = new List<FeaturePermissionRule>
            {
                // 1. Phân khu COMMON (Chung & Cổng Phụ huynh)
                new() { FeatureCode = "COM_LOOKUP", MasterCode = "COMMON", DisplayName = "Tra Cứu Học Viên & Điểm Danh", Description = "Xem kết quả đi lễ, đi học từng ngày, bảng điểm & báo sai sót điểm danh", IconClass = "bi-search-heart-fill", TargetUrl = "/TraCuu", BadgeColor = "primary", AllowedRolesCsv = "ALL", SortOrder = 1 },
                new() { FeatureCode = "COM_REGISTRATION", MasterCode = "COMMON", DisplayName = "Đăng Ký Tuyển Sinh Đầu Năm", Description = "Nộp hồ sơ đăng ký học Giáo lý trực tuyến cho thiếu nhi mới", IconClass = "bi-person-plus-fill", TargetUrl = "/PublicRegistration/Index", BadgeColor = "success", AllowedRolesCsv = "ALL", SortOrder = 2 },
                new() { FeatureCode = "COM_REG_STATUS", MasterCode = "COMMON", DisplayName = "Tra Cứu Hồ Sơ Tuyển Sinh", Description = "Kiểm tra kết quả xét duyệt và lớp xếp cho hồ sơ đăng ký mới", IconClass = "bi-clipboard2-check-fill", TargetUrl = "/PublicRegistration/CheckStatus", BadgeColor = "info", AllowedRolesCsv = "ALL", SortOrder = 3 },
                new() { FeatureCode = "COM_ANNOUNCEMENTS", MasterCode = "COMMON", DisplayName = "Bảng Tin & Dặn Dò Giáo Xứ", Description = "Xem và đăng thông báo chung toàn xứ, theo khối hoặc theo lớp học", IconClass = "bi-megaphone-fill", TargetUrl = "/Announcements/Index", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 4 },
                new() { FeatureCode = "COM_PARENT_MSG", MasterCode = "COMMON", DisplayName = "Mẫu Tin Nhắn Phụ Huynh 1-Click", Description = "Sao chép nhanh tin nhắn báo vắng, nhắc nhở gửi Zalo Phụ huynh miễn phí", IconClass = "bi-chat-quote-fill", TargetUrl = "/ParentMessages/Index", BadgeColor = "secondary", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 5 },

                // 2. Phân khu ATTENDANCE (Điểm danh & Chuyên cần)
                new() { FeatureCode = "ATT_QR_SCAN", MasterCode = "ATTENDANCE", DisplayName = "Quét QR Điểm Danh (Online/Offline)", Description = "Mở camera quét thẻ QR thiếu nhi tốc độ cao, tự lưu IndexedDB khi mất mạng", IconClass = "bi-qr-code-scan", TargetUrl = "/Attendance/ScanQr", BadgeColor = "primary", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 1 },
                new() { FeatureCode = "ATT_CLASS_BOOK", MasterCode = "ATTENDANCE", DisplayName = "Sổ Điểm Danh Lớp & Duyệt Sai Sót", Description = "Điểm danh Lễ Thứ Năm, Lễ Chúa Nhật, Học Giáo lý & duyệt báo sai sót từ PH", IconClass = "bi-calendar2-check-fill", TargetUrl = "/Attendance/Index", BadgeColor = "success", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 2 },
                new() { FeatureCode = "ATT_MASS_CAMPAIGN", MasterCode = "ATTENDANCE", DisplayName = "Chiến Dịch Đi Lễ 1 Tháng (Lớp Bí Tích)", Description = "Thiết lập & điểm danh ma trận 30 ngày liên tục cho lớp RL2, TS3, BĐ3, DB4", IconClass = "bi-calendar2-heart-fill", TargetUrl = "/MassCampaigns/Index", BadgeColor = "danger", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SacramentClassesOnly = true, SortOrder = 3 },
                new() { FeatureCode = "ATT_MONTHLY_STAT", MasterCode = "ATTENDANCE", DisplayName = "Tổng Hợp Điểm Danh Tháng", Description = "Xem ma trận chuyên cần theo tháng và xuất báo cáo Excel từng lớp", IconClass = "bi-calendar-month-fill", TargetUrl = "/Attendance/MonthlySummary", BadgeColor = "info", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 4 },
                new() { FeatureCode = "ATT_STATISTICS", MasterCode = "ATTENDANCE", DisplayName = "Báo Cáo Thống Kê Chuyên Cần Năm", Description = "Thống kê tổng hợp tỷ lệ đi lễ, đi học và xét điều kiện Bí tích", IconClass = "bi-pie-chart-fill", TargetUrl = "/Attendance/Statistics", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead", SortOrder = 5 },
                new() { FeatureCode = "ATT_ABSENCE_REQ", MasterCode = "ATTENDANCE", DisplayName = "Duyệt Đơn Xin Phép Nghỉ Học", Description = "Xem và phê duyệt đơn xin nghỉ phép trực tuyến từ phụ huynh", IconClass = "bi-envelope-paper-fill", TargetUrl = "/Absence/Manage", BadgeColor = "secondary", AllowedRolesCsv = "Admin,ExecutiveBoard,BranchHead,Teacher", SortOrder = 6 },

                // 3. Phân khu ACADEMIC (Học vụ & Bảng điểm)
                new() { FeatureCode = "ACD_GRADEBOOK", MasterCode = "ACADEMIC", DisplayName = "Sổ Điểm Giáo Lý & Xét Duyệt Cuối Năm", Description = "Nhập điểm HK1, HK2 và bấm Xét Lên Lớp / Xét Bí Tích / Xét Tuyên Hứa", IconClass = "bi-journal-check", TargetUrl = "/Grades/Index", BadgeColor = "primary", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 1 },
                new() { FeatureCode = "ACD_ATTENDANCE_DIRECT", MasterCode = "ACADEMIC", DisplayName = "Điểm Danh Lớp", Description = "Mở trực tiếp Sổ điểm danh lớp học được phân công", IconClass = "bi-calendar2-check-fill", TargetUrl = "/Attendance/Index?classId={class_id}", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 2 },
                new() { FeatureCode = "ACD_STUDENTS", MasterCode = "ACADEMIC", DisplayName = "Hồ Sơ Thiếu Nhi & In Thẻ QR", Description = "Quản lý danh sách thiếu nhi, sinh mã theo tên lớp (TS3A001) & in thẻ QR", IconClass = "bi-people-fill", TargetUrl = "/Students/Index", BadgeColor = "success", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 3 },
                new() { FeatureCode = "ACD_CLASSES", MasterCode = "ACADEMIC", DisplayName = "Quản Lý Lớp Học & Phân Công GLV", Description = "Danh sách các lớp Giáo lý, sĩ số, phân công Giáo lý viên đứng lớp", IconClass = "bi-mortarboard-fill", TargetUrl = "/Classes/Index", BadgeColor = "info", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 4 },
                new() { FeatureCode = "ACD_LESSON_PLANS", MasterCode = "ACADEMIC", DisplayName = "Kế Hoạch Bài Giảng & Giáo Án", Description = "Tải tài liệu bài giảng, phân phối chương trình học từng tuần", IconClass = "bi-calendar3-week-fill", TargetUrl = "/LessonPlans/Index", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 5 },
                new() { FeatureCode = "ACD_FACILITIES", MasterCode = "ACADEMIC", DisplayName = "Sơ Đồ Phòng Học & Cơ Sở Vật Chất", Description = "Sơ đồ phòng học các khu nhà, sĩ số thực tế và báo cáo sự cố thiết bị", IconClass = "bi-building-gear", TargetUrl = "/Facilities/Index", BadgeColor = "dark", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 6 },

                // 4. Phân khu SACRAMENT (Bí tích & Kết chuyển)
                new() { FeatureCode = "SAC_REGISTRY", MasterCode = "SACRAMENT", DisplayName = "Sổ Bộ Bí Tích Điện Tử", Description = "Quản lý sổ bộ Rửa Tội, Rước Lễ Lần Đầu, Thêm Sức, Tuyên Hứa Bao Đồng", IconClass = "bi-journal-bookmark-fill", TargetUrl = "/Sacraments/Index", BadgeColor = "success", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead,Teacher", SortOrder = 1 },
                new() { FeatureCode = "SAC_EVALUATION", MasterCode = "SACRAMENT", DisplayName = "Tờ Trình Xét Duyệt Bí Tích Cha Xứ", Description = "Tổng hợp tờ trình từ các lớp RL2, TS3, BĐ3, DB4 để Cha Xứ phê duyệt", IconClass = "bi-award-fill", TargetUrl = "/Sacraments/Evaluation", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead", SortOrder = 2 },
                new() { FeatureCode = "SAC_PROMOTION", MasterCode = "SACRAMENT", DisplayName = "Kết Chuyển Lên Lớp & Đổi Mã Năm Cũ", Description = "Xét lên lớp tự động và chuyển StudentCode gắn đuôi năm học cũ (_2526)", IconClass = "bi-arrow-up-right-circle-fill", TargetUrl = "/Promotion/Index", BadgeColor = "primary", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard", SortOrder = 3 },
                new() { FeatureCode = "SAC_ADMISSIONS", MasterCode = "SACRAMENT", DisplayName = "Duyệt Hồ Sơ Tuyển Sinh Đầu Năm", Description = "Xét duyệt đơn đăng ký trực tuyến, xem Giấy Rửa Tội và tự động xếp lớp", IconClass = "bi-person-vcard-fill", TargetUrl = "/StudentRegistrations/Index", BadgeColor = "info", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard", SortOrder = 4 },

                // 5. Phân khu GOVERNANCE (Quản trị & Hệ thống)
                new() { FeatureCode = "GOV_DASHBOARD", MasterCode = "GOVERNANCE", DisplayName = "Executive Dashboard Toàn Xứ", Description = "Biểu đồ chuyên cần theo khối, Heatmap vắng lễ dành cho Cha Xứ & BĐH", IconClass = "bi-speedometer2", TargetUrl = "/Dashboard/Index", BadgeColor = "primary", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead", SortOrder = 1 },
                new() { FeatureCode = "GOV_USERS", MasterCode = "GOVERNANCE", DisplayName = "Quản Lý Tài Khoản & Nhân Sự GLV", Description = "Tạo tài khoản Giáo lý viên, nạp hàng loạt sau tuyên hứa và phân quyền", IconClass = "bi-person-gear", TargetUrl = "/Users/Index", BadgeColor = "success", AllowedRolesCsv = "Admin,ExecutiveBoard", SortOrder = 2 },
                new() { FeatureCode = "GOV_ROTATION", MasterCode = "GOVERNANCE", DisplayName = "Bàn Giao Nhiệm Kỳ & Trưởng Khối", Description = "Luân chuyển Trưởng khối và bàn giao vai trò Ban Điều Hành qua các năm", IconClass = "bi-person-badge-fill", TargetUrl = "/Governance/Index", BadgeColor = "warning", AllowedRolesCsv = "Admin,SpiritualDirector,ExecutiveBoard", SortOrder = 3 },
                new() { FeatureCode = "GOV_FEATURE_PERMS", MasterCode = "GOVERNANCE", DisplayName = "Phân Quyền Nút Chức Năng (Master UI)", Description = "Cấu hình ẩn/hiện các nút nghiệp vụ theo Vai trò và theo từng Lớp học", IconClass = "bi-sliders2-vertical", TargetUrl = "/FeaturePermissions/Index", BadgeColor = "danger", AllowedRolesCsv = "Admin,ExecutiveBoard", SortOrder = 4 },
                new() { FeatureCode = "GOV_ACADEMIC_YEARS", MasterCode = "GOVERNANCE", DisplayName = "Quản Lý Niên Khóa Giáo Lý", Description = "Thiết lập năm học hiện hành và cấu hình khung thời gian năm học", IconClass = "bi-calendar3", TargetUrl = "/AcademicYears/Index", BadgeColor = "info", AllowedRolesCsv = "Admin,ExecutiveBoard", SortOrder = 5 },
                new() { FeatureCode = "GOV_SYSTEM_LOGS", MasterCode = "GOVERNANCE", DisplayName = "System Health & Nhật Ký Hệ Thống", Description = "Giám sát kết nối PostgreSQL/Redis, tra cứu Audit Log và lỗi hệ thống", IconClass = "bi-terminal-fill", TargetUrl = "/Admin/Logs", BadgeColor = "dark", AllowedRolesCsv = "Admin", SortOrder = 6 }
            };

            bool changed = false;
            foreach (var rule in defaults)
            {
                if (!existingMap.TryGetValue(rule.FeatureCode, out var existing))
                {
                    _context.FeaturePermissionRules.Add(rule);
                    changed = true;
                }
                else
                {
                    if (rule.FeatureCode == "ACD_ATTENDANCE_DIRECT")
                    {
                        if (existing.TargetUrl != rule.TargetUrl || existing.SortOrder != 2 || !existing.IsEnabled)
                        {
                            existing.TargetUrl = rule.TargetUrl;
                            existing.SortOrder = 2;
                            existing.IsEnabled = true;
                            changed = true;
                        }
                    }
                    else if (rule.MasterCode == "ACADEMIC" && existing.SortOrder != rule.SortOrder)
                    {
                        existing.SortOrder = rule.SortOrder;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<MasterWorkspaceViewModel> GetAuthorizedWorkspaceAsync(
            ClaimsPrincipal user,
            string? activeMasterCode = "COMMON")
        {
            await EnsureDefaultRulesSeededAsync();

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            int totalStudents = await _context.Students.CountAsync(s => s.IsActive);
            int totalClasses = 0;
            int totalEnrolled = 0;

            if (currentYear != null)
            {
                totalClasses = await _context.Classes.CountAsync(c => c.AcademicYearId == currentYear.Id);
                totalEnrolled = await _context.Enrollments.CountAsync(e => e.ClassRoom.AcademicYearId == currentYear.Id && e.Student.IsActive);
            }

            int pendingRegs = await _context.StudentRegistrationRequests.CountAsync(r => r.Status == "PENDING");

            var vm = new MasterWorkspaceViewModel
            {
                IsAuthenticated = user?.Identity?.IsAuthenticated == true,
                CurrentAcademicYearName = currentYear?.Name ?? "Chưa thiết lập",
                TotalStudents = totalStudents,
                TotalClasses = totalClasses,
                TotalEnrolled = totalEnrolled,
                PendingRegistrations = pendingRegs
            };

            string currentRole = "Anonymous";
            long currentUserId = 0;
            var userClassIds = new HashSet<int>();
            bool hasSacramentClass = false;

            if (vm.IsAuthenticated && user != null)
            {
                vm.CurrentUserFullName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";
                currentRole = user.FindFirstValue(ClaimTypes.Role) ?? UserRole.Teacher;
                vm.CurrentUserRoleDisplay = GetRoleVietnameseLabel(currentRole);

                long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);

                if (currentYear != null)
                {
                    if (currentRole == UserRole.Admin ||
                        currentRole == UserRole.SpiritualDirector ||
                        currentRole == UserRole.ExecutiveBoard)
                    {
                        hasSacramentClass = true;
                    }
                    else if (currentRole == UserRole.BranchHead)
                    {
                        var managedGrade = user.FindFirst("ManagedGradeLevel")?.Value ?? "";
                        if (!string.IsNullOrWhiteSpace(managedGrade))
                        {
                            vm.AssignedClassNames.Add($"Khối {managedGrade}");
                            var branchClasses = await _context.Classes
                                .AsNoTracking()
                                .Where(c => c.AcademicYearId == currentYear.Id && c.GradeLevel == managedGrade)
                                .ToListAsync();

                            foreach (var c in branchClasses)
                            {
                                userClassIds.Add(c.Id);
                                if (IsSacramentClassName(c.Name, c.GradeLevel))
                                    hasSacramentClass = true;
                            }
                        }
                    }
                    else if (currentUserId > 0)
                    {
                        var assignedClasses = await _context.ClassTeachers
                            .AsNoTracking()
                            .Include(ct => ct.ClassRoom)
                            .Where(ct => ct.UserId == currentUserId && ct.ClassRoom != null && ct.ClassRoom.AcademicYearId == currentYear.Id)
                            .Select(ct => ct.ClassRoom!)
                            .ToListAsync();

                        foreach (var cls in assignedClasses)
                        {
                            userClassIds.Add(cls.Id);
                            vm.AssignedClassNames.Add(cls.Name);
                            if (IsSacramentClassName(cls.Name, cls.GradeLevel))
                                hasSacramentClass = true;
                        }
                    }
                }
            }

            var allEnabledRules = await _context.FeaturePermissionRules
                .AsNoTracking()
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.SortOrder)
                .ToListAsync();

            var masterDefinitions = new List<MasterTabDto>
            {
                new() { MasterCode = "COMMON", Title = "Chung & Cổng Thông Tin", ShortTitle = "Chung", IconClass = "bi-house-heart-fill", Description = "Tra cứu điểm danh, thông báo lớp học & tiện ích chung" },
                new() { MasterCode = "ATTENDANCE", Title = "Điểm Danh & Chiến Dịch Lễ", ShortTitle = "Điểm Danh", IconClass = "bi-qr-code-scan", Description = "Quét QR, sổ điểm danh lớp & chiến dịch đi lễ 1 tháng" },
                new() { MasterCode = "ACADEMIC", Title = "Học Vụ & Sổ Điểm Giáo Lý", ShortTitle = "Học Vụ", IconClass = "bi-mortarboard-fill", Description = "Sổ điểm, xét duyệt cuối năm, hồ sơ thiếu nhi & giáo án" },
                new() { MasterCode = "SACRAMENT", Title = "Bí Tích & Kết Chuyển Năm", ShortTitle = "Bí Tích", IconClass = "bi-award-fill", Description = "Sổ bộ Bí tích, tờ trình Cha Xứ & kết chuyển lên lớp" },
                new() { MasterCode = "GOVERNANCE", Title = "Quản Trị & Điều Hành", ShortTitle = "Quản Trị", IconClass = "bi-shield-lock-fill", Description = "Dashboard toàn xứ, nhân sự GLV & phân quyền nút chức năng" }
            };

            bool isFullAccessRole = currentRole == UserRole.Admin ||
                                    currentRole == UserRole.SpiritualDirector ||
                                    currentRole == UserRole.ExecutiveBoard;

            // Xác định classId mặc định được phân quyền cho User đang đăng nhập
            int? defaultClassId = null;
            if (userClassIds.Any())
            {
                defaultClassId = userClassIds.First();
            }
            else if (currentYear != null)
            {
                var firstClass = await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.AcademicYearId == currentYear.Id)
                    .OrderBy(c => c.GradeLevel)
                    .ThenBy(c => c.Name)
                    .FirstOrDefaultAsync();
                defaultClassId = firstClass?.Id;
            }

            foreach (var master in masterDefinitions)
            {
                var rulesInMaster = allEnabledRules.Where(r => r.MasterCode == master.MasterCode).ToList();

                foreach (var rule in rulesInMaster)
                {
                    if (!IsRuleVisibleForUser(rule, vm.IsAuthenticated, currentRole, isFullAccessRole, hasSacramentClass, userClassIds))
                        continue;

                    // Thay thế tham số {class_id} động theo quyền lớp của tài khoản đang đăng nhập
                    string finalTargetUrl = rule.TargetUrl;
                    if (finalTargetUrl.Contains("{class_id}"))
                    {
                        finalTargetUrl = defaultClassId.HasValue && defaultClassId.Value > 0
                            ? finalTargetUrl.Replace("{class_id}", defaultClassId.Value.ToString())
                            : "/Attendance/Index";
                    }
                    else if (rule.FeatureCode == "ACD_ATTENDANCE_DIRECT")
                    {
                        finalTargetUrl = defaultClassId.HasValue && defaultClassId.Value > 0
                            ? $"/Attendance/Index?classId={defaultClassId.Value}"
                            : "/Attendance/Index";
                    }

                    master.Features.Add(new FeatureActionCardDto
                    {
                        FeatureCode = rule.FeatureCode,
                        MasterCode = rule.MasterCode,
                        DisplayName = rule.DisplayName,
                        Description = rule.Description ?? string.Empty,
                        IconClass = rule.IconClass,
                        TargetUrl = finalTargetUrl,
                        BadgeColor = rule.BadgeColor,
                        SacramentClassesOnly = rule.SacramentClassesOnly
                    });
                }

                if (master.MasterCode == "COMMON" || master.Features.Any())
                {
                    vm.Masters.Add(master);
                }
            }

            var requestedMaster = (activeMasterCode ?? "COMMON").Trim().ToUpperInvariant();
            vm.ActiveMasterCode = vm.Masters.Any(m => m.MasterCode == requestedMaster)
                ? requestedMaster
                : "COMMON";

            var nowUtc = DateTime.UtcNow;
            vm.PinAnnouncements = await _context.Announcements
                .AsNoTracking()
                .Where(a => a.IsActive &&
                            (a.Scope == "ALL" || a.Scope == "PARISH") &&
                            (!a.ExpireAt.HasValue || a.ExpireAt.Value >= nowUtc))
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync();

            return vm;
        }

        public async Task<List<FeaturePermissionRule>> GetAllRulesForManagementAsync()
        {
            await EnsureDefaultRulesSeededAsync();
            return await _context.FeaturePermissionRules
                .AsNoTracking()
                .OrderBy(r => r.MasterCode)
                .ThenBy(r => r.SortOrder)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message)> UpdatePermissionRulesBatchAsync(
            List<SaveFeaturePermissionItemDto> items,
            string updatedBy,
            string ipAddress)
        {
            if (items == null || !items.Any())
                return (false, "Không có dữ liệu cấu hình để lưu.");

            var rules = await _context.FeaturePermissionRules.ToDictionaryAsync(r => r.Id);

            foreach (var item in items)
            {
                if (!rules.TryGetValue(item.Id, out var rule))
                    continue;

                var roles = (item.AllowedRoles ?? new List<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct()
                    .ToList();

                rule.AllowedRolesCsv = roles.Any() ? string.Join(",", roles) : "Admin";
                rule.SacramentClassesOnly = item.SacramentClassesOnly;
                rule.SpecificClassIdsCsv = string.IsNullOrWhiteSpace(item.SpecificClassIdsCsv)
                    ? null
                    : item.SpecificClassIdsCsv.Trim();
                rule.IsEnabled = item.IsEnabled;
                rule.SortOrder = item.SortOrder;
                rule.UpdatedBy = updatedBy;
                rule.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT UPDATE_FEATURE_PERMISSIONS: User={User} updated {Count} feature rules from IP={IP}",
                updatedBy, items.Count, ipAddress);

            return (true, "Đã lưu cấu hình phân quyền hiển thị nút chức năng thành công!");
        }

        private static bool IsRuleVisibleForUser(
            FeaturePermissionRule rule,
            bool isAuthenticated,
            string currentRole,
            bool isFullAccessRole,
            bool hasSacramentClass,
            HashSet<int> userClassIds)
        {
            var allowedRoles = rule.AllowedRolesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowedRoles.Contains("ALL"))
                return true;

            if (!isAuthenticated)
                return false;

            if (currentRole == UserRole.Admin)
                return true;

            if (!allowedRoles.Contains(currentRole))
                return false;

            if (!string.IsNullOrWhiteSpace(rule.SpecificClassIdsCsv) && !isFullAccessRole)
            {
                var requiredClassIds = rule.SpecificClassIdsCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : -1)
                    .Where(id => id > 0)
                    .ToHashSet();

                if (requiredClassIds.Any() && !requiredClassIds.Overlaps(userClassIds))
                    return false;
            }

            if (rule.SacramentClassesOnly && !isFullAccessRole && !hasSacramentClass)
                return false;

            return true;
        }

        private static bool IsSacramentClassName(string? className, string? gradeLevel)
        {
            var clean = RemoveDiacritics(className ?? "").ToUpperInvariant();
            var grade = RemoveDiacritics(gradeLevel ?? "").ToUpperInvariant();
            return clean.Contains("RUOC LE 2") || clean.StartsWith("RL2") || clean.Contains("RL 2") ||
                   clean.Contains("THEM SUC 3") || clean.StartsWith("TS3") || clean.Contains("TS 3") ||
                   clean.Contains("BAO DONG 3") || clean.StartsWith("BD3") || clean.Contains("BD 3") ||
                   clean.Contains("DU BI 4") || clean.StartsWith("DB4") || clean.Contains("DB 4") || grade.Contains("DU BI");
        }

        private static string GetRoleVietnameseLabel(string role) => role switch
        {
            UserRole.Admin => "Quản Trị Hệ Thống",
            UserRole.SpiritualDirector => "Cha Tuyên Úy",
            UserRole.ExecutiveBoard => "Ban Điều Hành",
            UserRole.BranchHead => "Trưởng Khối",
            UserRole.Teacher => "Giáo Lý Viên",
            _ => "Thành viên"
        };

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }
}