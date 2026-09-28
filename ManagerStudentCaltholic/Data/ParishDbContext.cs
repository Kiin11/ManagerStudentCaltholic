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

            // Ràng buộc duy nhất: 1 học sinh chỉ có 1 bản ghi điểm danh trong 1 ngày
            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.EnrollmentId, a.AttendanceDate }).IsUnique();

            // Đánh B-Tree Composite Index tối ưu truy vấn tìm kiếm điểm danh theo ngày
            modelBuilder.Entity<Attendance>()
                .HasIndex(a => a.AttendanceDate);

            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.Property(a => a.MassAttended).HasDefaultValue(false);
                entity.Property(a => a.ClassAttended).HasDefaultValue(false);
                entity.Property(a => a.IsMakeUp).HasDefaultValue(false);
                entity.Property(a => a.Status).HasMaxLength(20).HasDefaultValue("PRESENT");
                entity.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
            });
        }
    }
}
