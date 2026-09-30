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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

                // Ràng buộc duy nhất: Một GLV chỉ nhận 1 vai trò phân công trong 1 lớp của 1 niên khóa
                entity.HasIndex(ct => new { ct.ClassRoomId, ct.TeacherName, ct.AcademicYearId })
                      .IsUnique();

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
        }
    }
}
