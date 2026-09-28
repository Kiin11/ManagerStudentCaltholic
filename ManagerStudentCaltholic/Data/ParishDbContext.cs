using ManagerStudentCaltholic.Model.Entities;
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
        }
    }
}
