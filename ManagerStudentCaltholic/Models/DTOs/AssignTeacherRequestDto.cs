namespace ManagerStudentCaltholic.Models.DTOs
{
    public class AssignTeacherRequestDto
    {
        public int ClassRoomId { get; set; }
        public long UserId { get; set; }
        public string RoleInClass { get; set; } = "HEAD";
    }
}
