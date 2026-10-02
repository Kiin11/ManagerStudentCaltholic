namespace ManagerStudentCaltholic.Models.DTOs
{
    // DTO tiếp nhận yêu cầu gán phòng
    public class AssignRoomRequestDto
    {
        public int ClassId { get; set; }
        public int LocationId { get; set; }
    }
}
