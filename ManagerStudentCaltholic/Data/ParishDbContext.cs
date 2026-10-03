using ManagerStudentCaltholic.Interface.Models;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Data
{
    public class ParishDbContext : DbContext
    {
        public ParishDbContext(DbContextOptions<ParishDbContext> options) : base(options) { }

        public DbSet<Student> Students => Set<Student>();
        public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
        public DbSet<ClassRoom> Classes => Set<ClassRoom>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<AttendanceAuditLog> AttendanceAuditLogs => Set<AttendanceAuditLog>();
        public DbSet<ClassTeacher> ClassTeachers => Set<ClassTeacher>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Announcement> Announcements => Set<Announcement>();

        // --- Facilities & Logistics (Epic 9) ---
        public DbSet<BuildingZone> BuildingZones => Set<BuildingZone>();
        public DbSet<ClassRoomLocation> ClassRoomLocations => Set<ClassRoomLocation>();
        public DbSet<RoomIncidentReport> RoomIncidentReports => Set<RoomIncidentReport>();
        public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();

        // --- Role & Governance (Epic 9: US-9.4 & US-9.5) ---
        public DbSet<BranchHeadAssignment> BranchHeadAssignments => Set<BranchHeadAssignment>();
        public DbSet<UserRoleHistory> UserRoleHistories => Set<UserRoleHistory>();
        
        // --- Syllabus & Lesson Plans (Epic 9: TASK-904) ---
        //public DbSet<ClassLessonPlan> ClassLessonPlans => Set<ClassLessonPlan>();

        public DbSet<ClassLessonDocument> ClassLessonDocuments => Set<ClassLessonDocument>();

        // --- Grade Configuration & Records (Epic 10: TASK-1001, TASK-1002) ---
        public DbSet<GradeConfiguration> GradeConfigurations => Set<GradeConfiguration>();
        public DbSet<GradeRecord> GradeRecords => Set<GradeRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // TASK-710: Thiết lập Global Query Filter (Tự động lọc các bản ghi đã xóa mềm)
            modelBuilder.Entity<Student>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ClassRoom>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Enrollment>().HasQueryFilter(e => !e.IsDeleted);

            // Đánh index cho trường IsDeleted để truy vấn cực nhanh
            modelBuilder.Entity<Student>().HasIndex(e => e.IsDeleted);
            modelBuilder.Entity<ClassRoom>().HasIndex(e => e.IsDeleted);
            modelBuilder.Entity<Enrollment>().HasIndex(e => e.IsDeleted);

            modelBuilder.Entity<ClassRoom>(entity =>
            {
                entity.Property(c => c.IsGradeLocked).HasDefaultValue(false);
            });

            // Ràng buộc duy nhất: 1 học sinh chỉ xếp vào 1 lớp trong cùng 1 niên khóa
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.ClassRoomId }).IsUnique();

            modelBuilder.Entity<Attendance>(entity =>
            {
                // Đảm bảo 1 học sinh chỉ có 1 dòng ghi nhận trong 1 ngày
                entity.HasIndex(a => new { a.EnrollmentId, a.AttendanceDate }).IsUnique();

                // B-Tree Index tra cứu theo ngày
                entity.HasIndex(a => a.AttendanceDate);

                // TỐI ƯU TRUY VẤN THEO THỨ: Đánh Index trên DayOfWeek và trạng thái đi Lễ
                entity.HasIndex(a => new { a.DayOfWeek, a.AttendedMass });
                entity.HasIndex(a => a.DayOfWeek);

                entity.Property(a => a.DayOfWeek).HasConversion<int>(); // Lưu vào Postgres dưới dạng số nguyên (0..6)
                entity.Property(a => a.AttendedMass).HasDefaultValue(false);
                entity.Property(a => a.MassStatus).HasMaxLength(20).HasDefaultValue("ABSENT_UNPERMITTED");
                entity.Property(a => a.ClassAttended).HasDefaultValue(false);
                entity.Property(a => a.ClassStatus).HasMaxLength(20).HasDefaultValue("ABSENT_UNPERMITTED");
                entity.Property(a => a.IsMakeUp).HasDefaultValue(false);
                entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<AttendanceAuditLog>(entity =>
            {
                entity.ToTable("AttendanceAuditLogs");

                entity.HasKey(a => a.Id);

                // Khóa ngoại liên kết với Attendance (1 bản ghi điểm danh có nhiều lượt log)
                entity.HasOne(a => a.Attendance)
                      .WithMany()
                      .HasForeignKey(a => a.AttendanceId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Đánh Index để tối ưu truy vấn xem lịch sử theo điểm danh và theo thời gian
                entity.HasIndex(a => a.AttendanceId);
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => a.ActionType);

                entity.Property(a => a.ActionType).HasMaxLength(50).IsRequired();
                entity.Property(a => a.ModifiedBy).HasMaxLength(100).HasDefaultValue("SYSTEM");
                entity.Property(a => a.Reason).HasMaxLength(255);
                entity.Property(a => a.IpAddress).HasMaxLength(45);
                entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<ClassTeacher>(entity =>
            {
                entity.ToTable("ClassTeachers");
                entity.HasKey(ct => ct.Id);

                // Chỉ định rõ ràng: ClassTeacher liên kết tới User qua khóa ngoại UserId
                entity.HasOne(ct => ct.User)
                      .WithMany()
                      .HasForeignKey(ct => ct.UserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(ct => ct.ClassRoom)
                      .WithMany(c => c.ClassTeachers)
                      .HasForeignKey(ct => ct.ClassRoomId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ct => ct.AcademicYear)
                      .WithMany()
                      .HasForeignKey(ct => ct.AcademicYearId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(ct => ct.TeacherName).HasMaxLength(100).IsRequired();
                entity.Property(ct => ct.RoleInClass).HasMaxLength(20).HasDefaultValue("HEAD");
                entity.Property(ct => ct.AssignedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);

                // Username là duy nhất trên toàn hệ thống
                entity.HasIndex(u => u.Username).IsUnique();

                // Index tìm kiếm nhanh theo Email / SĐT / Role
                entity.HasIndex(u => u.Email);
                entity.HasIndex(u => u.PhoneNumber);
                entity.HasIndex(u => u.Role);

                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(u => u.LastLoginAt).HasMaxLength(10); 
                entity.Property(u => u.ChristianName).HasMaxLength(50);
                entity.Property(u => u.Address).HasMaxLength(200);
                entity.Property(u => u.AvatarUrl).HasMaxLength(255);
                entity.Property(u => u.Role).HasMaxLength(30).HasDefaultValue(UserRole.Teacher);
                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.AccessFailedCount).HasDefaultValue(0);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(u => u.ManagedGradeLevel).HasMaxLength(50).IsRequired(false);

                // Liên kết 1 - 1 / 1 - N tùy chọn với ClassTeacher và Student
                entity.HasOne(u => u.ClassTeacher)
                      .WithMany()
                      .HasForeignKey(u => u.ClassTeacherId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(u => u.Student)
                      .WithMany()
                      .HasForeignKey(u => u.StudentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(u => u.ManagedGradeLevel);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(rt => rt.Id);

                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasIndex(rt => rt.UserId);

                entity.HasOne(rt => rt.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(rt => rt.Token).HasMaxLength(255).IsRequired();
                entity.Property(rt => rt.CreatedAt).HasDefaultValueSql("NOW()");
            });
            modelBuilder.Entity<Announcement>(entity =>
            {
                entity.ToTable("Announcements");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
                entity.Property(a => a.Scope).HasMaxLength(20).HasDefaultValue("ALL");
                entity.Property(a => a.Priority).HasMaxLength(20).HasDefaultValue("NORMAL");
                entity.Property(a => a.IsPinned).HasDefaultValue(false);
                entity.Property(a => a.IsActive).HasDefaultValue(true);
                entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");

                // Quan hệ tùy chọn với lớp học khi Scope = CLASS
                entity.HasOne(a => a.TargetClassRoom)
                      .WithMany()
                      .HasForeignKey(a => a.TargetClassRoomId)
                      .OnDelete(DeleteBehavior.SetNull);

                // B-Tree Indexes phục vụ lọc tin tức theo phạm vi, thời gian và ghim bài
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => a.Scope);
                entity.HasIndex(a => a.TargetGradeLevel);
                entity.HasIndex(a => a.IsPinned);
            });

            // ==========================================
            // 4. FACILITIES & LOGISTICS (EPIC 9)
            // ==========================================
            modelBuilder.Entity<BuildingZone>(entity =>
            {
                entity.ToTable("BuildingZones");
                entity.HasKey(z => z.Id);
                entity.Property(z => z.ZoneName).HasMaxLength(100).IsRequired();
                entity.Property(z => z.ZoneCode).HasMaxLength(20);
                entity.Property(z => z.DisplayOrder).HasDefaultValue(1);
            });

            modelBuilder.Entity<ClassRoomLocation>(entity =>
            {
                entity.ToTable("ClassRoomLocations");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.RoomName).HasMaxLength(50).IsRequired();
                entity.Property(l => l.FloorNumber).HasDefaultValue(1);
                entity.Property(l => l.Capacity).HasDefaultValue(40);
                entity.Property(l => l.IsAvailable).HasDefaultValue(true);

                entity.HasOne(l => l.BuildingZone)
                      .WithMany(z => z.Rooms)
                      .HasForeignKey(l => l.BuildingZoneId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(l => new { l.BuildingZoneId, l.FloorNumber });
            });

            // 4.1. ClassSchedule (TASK-904)
            modelBuilder.Entity<ClassSchedule>(entity =>
            {
                entity.ToTable("ClassSchedules");
                entity.HasKey(s => s.Id);

                entity.HasOne(s => s.ClassRoom)
                      .WithMany()
                      .HasForeignKey(s => s.ClassRoomId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.ClassRoomLocation)
                      .WithMany()
                      .HasForeignKey(s => s.ClassRoomLocationId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(s => s.DayOfWeek).HasConversion<int>();
                entity.Property(s => s.Notes).HasMaxLength(255);

                entity.HasIndex(s => new { s.ClassRoomLocationId, s.DayOfWeek, s.StartTime });
                entity.HasIndex(s => s.ClassRoomId);
            });

            // 4.2. RoomIncidentReport (TASK-905)
            modelBuilder.Entity<RoomIncidentReport>(entity =>
            {
                entity.ToTable("RoomIncidentReports");
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.ClassRoomLocation)
                      .WithMany()
                      .HasForeignKey(r => r.ClassRoomLocationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.ReporterUser)
                      .WithMany()
                      .HasForeignKey(r => r.ReporterUserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(r => r.DeviceType).HasMaxLength(50).IsRequired();
                entity.Property(r => r.Severity).HasMaxLength(20).HasDefaultValue("NORMAL");
                entity.Property(r => r.Description).HasMaxLength(500).IsRequired();
                entity.Property(r => r.Status).HasMaxLength(30).HasDefaultValue("PENDING");
                entity.Property(r => r.ResolutionNotes).HasMaxLength(255);
                entity.Property(r => r.ReportedAt).HasDefaultValueSql("NOW()");

                entity.HasIndex(r => new { r.ClassRoomLocationId, r.Status });
                entity.HasIndex(r => r.ReporterUserId);
            });
            // ==========================================
            // 4.3. ClassLessonPlan (TASK-904: Kế hoạch năm học & Bài giảng theo tuần)
            // ==========================================
            modelBuilder.Entity<ClassLessonPlan>(entity =>
            {
                entity.ToTable("ClassLessonPlans");
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.ClassRoom)
                      .WithMany()
                      .HasForeignKey(p => p.ClassRoomId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.AcademicYear)
                      .WithMany()
                      .HasForeignKey(p => p.AcademicYearId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.AssignedTeacherUser)
                      .WithMany()
                      .HasForeignKey(p => p.AssignedTeacherUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(p => p.LiturgicalDay).HasMaxLength(100);
                entity.Property(p => p.LessonCode).HasMaxLength(50);
                entity.Property(p => p.TopicTitle).HasMaxLength(255).IsRequired();
                entity.Property(p => p.AssignedTeacherName).HasMaxLength(100);
                entity.Property(p => p.EventNotes).HasMaxLength(500);
                entity.Property(p => p.IsExamDay).HasDefaultValue(false);
                entity.Property(p => p.IsDayOff).HasDefaultValue(false);

                // Index phục vụ load nhanh giáo án tuần theo lớp và năm học
                entity.HasIndex(p => new { p.ClassRoomId, p.LessonDate });
                entity.HasIndex(p => p.AcademicYearId);
                entity.HasIndex(p => p.AssignedTeacherUserId);
            });

            // ==========================================
            // 5. GOVERNANCE & ROLES (TASK-906 & TASK-907)
            // ==========================================
            // 5.1. BranchHeadAssignment (TASK-906)
            modelBuilder.Entity<BranchHeadAssignment>(entity =>
            {
                entity.ToTable("BranchHeadAssignments");
                entity.HasKey(b => b.Id);

                entity.HasOne(b => b.User)
                      .WithMany()
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.AcademicYear)
                      .WithMany()
                      .HasForeignKey(b => b.AcademicYearId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.AssignedByUser)
                      .WithMany()
                      .HasForeignKey(b => b.AssignedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(b => b.ManagedGradeLevel).HasMaxLength(50).IsRequired();
                entity.Property(b => b.Notes).HasMaxLength(255);
                entity.Property(b => b.AssignedAt).HasDefaultValueSql("NOW()");

                // Ràng buộc duy nhất: Trong 1 niên khóa, 1 khối chỉ do 1 Trưởng khối phụ trách
                entity.HasIndex(b => new { b.AcademicYearId, b.ManagedGradeLevel }).IsUnique();
                entity.HasIndex(b => b.UserId);
            });

            // 5.2. UserRoleHistory (TASK-907)
            modelBuilder.Entity<UserRoleHistory>(entity =>
            {
                entity.ToTable("UserRoleHistory");
                entity.HasKey(h => h.Id);

                entity.HasOne(h => h.User)
                      .WithMany()
                      .HasForeignKey(h => h.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.AcademicYear)
                      .WithMany()
                      .HasForeignKey(h => h.AcademicYearId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(h => h.ChangedByUser)
                      .WithMany()
                      .HasForeignKey(h => h.ChangedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(h => h.OldRole).HasMaxLength(30).IsRequired();
                entity.Property(h => h.NewRole).HasMaxLength(30).IsRequired();
                entity.Property(h => h.Reason).HasMaxLength(255);
                entity.Property(h => h.ChangedAt).HasDefaultValueSql("NOW()");

                entity.HasIndex(h => new { h.UserId, h.AcademicYearId });
                entity.HasIndex(h => h.ChangedAt);
            });

            // 6. GRADE CONFIGURATION & RECORDS (TASK-1001, TASK-1002)
            modelBuilder.Entity<GradeConfiguration>(entity =>
            {
                entity.ToTable("GradeConfigurations");
                entity.HasKey(g => g.Id);

                entity.HasOne(g => g.AcademicYear)
                      .WithMany()
                      .HasForeignKey(g => g.AcademicYearId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(g => g.ClassRoom)
                      .WithMany()
                      .HasForeignKey(g => g.ClassRoomId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Index tìm kiếm cấu hình theo năm, học kỳ và khối
                entity.HasIndex(g => new { g.AcademicYearId, g.GradeLevel, g.Semester });
                entity.Property(g => g.WeightFactor).HasDefaultValue(1);
                entity.Property(g => g.IsSacramentExam).HasDefaultValue(false);
                entity.Property(g => g.IsRequired).HasDefaultValue(true);
                entity.Property(g => g.CreatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<GradeRecord>(entity =>
            {
                entity.ToTable("GradeRecords");
                entity.HasKey(r => r.Id);

                entity.HasOne(r => r.Enrollment)
                      .WithMany(e => e.GradeRecords)
                      .HasForeignKey(r => r.EnrollmentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.GradeConfiguration)
                      .WithMany(g => g.GradeRecords)
                      .HasForeignKey(r => r.GradeConfigurationId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Ràng buộc duy nhất: Một học sinh chỉ có 1 điểm duy nhất cho 1 cột cấu hình
                entity.HasIndex(r => new { r.EnrollmentId, r.GradeConfigurationId })
                      .IsUnique();

                entity.Property(r => r.UpdatedAt).HasDefaultValueSql("NOW()");
            });
        }

        /// <summary>
        /// TASK-710: Ghi đè SaveChangesAsync để khi gọi _context.Remove(), 
        /// EF Core tự động chuyển thành Soft Delete (cập nhật IsDeleted = true và DeletedAt)
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    // Chuyển hành vi Delete thành Modify
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
