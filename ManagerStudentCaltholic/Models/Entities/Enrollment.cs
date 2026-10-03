using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class Enrollment : ISoftDelete
    {
        public long Id { get; set; }

        public long StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>(); 
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        // Thêm vào trong class Enrollment:
        public ICollection<GradeRecord> GradeRecords { get; set; } = new List<GradeRecord>();
    }
}
