using ManagerStudentCaltholic.Interface.Models;
using System.ComponentModel.DataAnnotations;

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

        // Bổ sung vào trong class Enrollment (Models/Entities/Enrollment.cs):

        /// <summary>
        /// Cờ đánh dấu đủ điều kiện lên lớp cuối năm
        /// </summary>
        public bool IsPassed { get; set; } = true;

        /// <summary>
        /// Loại xét duyệt áp dụng cho lớp: PROMOTION_NORMAL | SACRAMENT_RL2 | SACRAMENT_TS3 | PROMISE_BD3_DB4
        /// </summary>
        [MaxLength(30)]
        public string? EvaluationActionType { get; set; }

        /// <summary>
        /// Trạng thái đề xuất của GLV: PENDING | PROPOSED_PASS | PROPOSED_SPECIAL | PROPOSED_RETAIN
        /// </summary>
        [MaxLength(30)]
        public string TeacherProposalStatus { get; set; } = "PENDING";

        /// <summary>
        /// Ghi chú nhận xét của GLV hoặc lý do xin đặc cách (chuẩn chước)
        /// </summary>
        [MaxLength(500)]
        public string? TeacherProposalNote { get; set; }

        /// <summary>
        /// Tên Thánh & Họ tên người đỡ đầu (dành cho lớp xét Bí tích RL2, TS3, BĐ3, DB4)
        /// </summary>
        [MaxLength(150)]
        public string? ProposedGodparentName { get; set; }

        [MaxLength(100)]
        public string? ProposedBy { get; set; }

        public DateTime? ProposedAt { get; set; }

        public int? AcademicYearId { get; set; }
    }
}
