namespace ManagerStudentCaltholic.Models.Entities
{
    public static class UserRole
    {
        // 1. Quản trị hệ thống kỹ thuật
        public const string Admin = "Admin";

        // 2. Lãnh đạo tinh thần & phê duyệt Bí tích
        public const string SpiritualDirector = "SpiritualDirector"; // Cha Tuyên Úy / Cha Xứ

        // 3. Ban Điều Hành xứ đoàn (Đoàn trưởng, Đoàn phó, Thư ký xứ đoàn)
        public const string ExecutiveBoard = "ExecutiveBoard";

        // 4. Phân đoàn trưởng / Trưởng khối ngành
        public const string BranchHead = "BranchHead";

        // 5. Giáo lý viên / Huynh trưởng phụ trách lớp
        public const string Teacher = "Teacher";

        // 6. Phụ huynh / Thiếu nhi
        public const string Parent = "Parent";
    }

    /// <summary>
    /// Nhóm các vai trò phục vụ khai báo Policy trong ASP.NET Core Authorization
    /// </summary>
    public static class RoleGroups
    {
        // Nhóm quản trị cao cấp: Admin, Cha Tuyên Úy, Ban Điều Hành
        public const string Leadership = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}";

        // Nhóm điều hành học vụ: Cha Tuyên Úy, Ban Điều Hành, Trưởng Khối
        public const string AcademicManagement = $"{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}";

        // Nhóm ban giảng dạy & quản lý: Tất cả các cấp từ GLV trở lên
        public const string Staff = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}";
    }
}
