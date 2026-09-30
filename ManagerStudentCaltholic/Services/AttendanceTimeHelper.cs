namespace ManagerStudentCaltholic.Services
{
    public static class AttendanceStatus
    {
        public const string Present = "PRESENT";
        public const string Late = "LATE";
        public const string AbsentPermitted = "ABSENT_PERMITTED";
        public const string AbsentUnpermitted = "ABSENT_UNPERMITTED";
    }

    public static class AttendanceTimeHelper
    {
        /// <summary>
        /// Phân tích thời điểm quét hoặc điểm danh để xác định loại ca và trạng thái đúng giờ/trễ
        /// </summary>
        public static (bool isMass, bool isClass, string calculatedStatus, TimeSpan timeOfDay) EvaluateCheckIn(DateTime checkTime, string gradeLevel)
        {
            var dayOfWeek = checkTime.DayOfWeek;
            var timeOfDay = checkTime.TimeOfDay;

            // 1. Thánh Lễ Thứ 5: Bắt đầu 17h45
            if (dayOfWeek == DayOfWeek.Thursday && timeOfDay >= new TimeSpan(17, 0, 0) && timeOfDay <= new TimeSpan(19, 0, 0))
            {
                var isLate = timeOfDay > new TimeSpan(18, 0, 0);
                return (isMass: true, isClass: false, isLate ? AttendanceStatus.Late : AttendanceStatus.Present, timeOfDay);
            }

            // 2. Thánh Lễ Sáng Chúa Nhật: Bắt đầu 06h30
            if (dayOfWeek == DayOfWeek.Sunday && timeOfDay >= new TimeSpan(6, 0, 0) && timeOfDay <= new TimeSpan(8, 15, 0))
            {
                var isLate = timeOfDay > new TimeSpan(6, 40, 0);
                return (isMass: true, isClass: false, isLate ? AttendanceStatus.Late : AttendanceStatus.Present, timeOfDay);
            }

            // 3. Giờ Học Giáo Lý Chúa Nhật
            if (dayOfWeek == DayOfWeek.Sunday)
            {
                // Khối sáng (Khai tâm, Rước lễ): 09h00
                if ((gradeLevel == "Khai Tâm" || gradeLevel == "Rước Lễ" || gradeLevel == "Thêm Sức") &&
                    timeOfDay >= new TimeSpan(8, 30, 0) && timeOfDay <= new TimeSpan(11, 0, 0))
                {
                    var isLate = timeOfDay > new TimeSpan(9, 10, 0);
                    return (isMass: false, isClass: true, isLate ? AttendanceStatus.Late : AttendanceStatus.Present, timeOfDay);
                }

                // Khối chiều (Bao đồng): 15h00
                if ((gradeLevel == "Bao Đồng") &&
                    timeOfDay >= new TimeSpan(14, 30, 0) && timeOfDay <= new TimeSpan(17, 10, 0))
                {
                    var isLate = timeOfDay > new TimeSpan(15, 10, 0);
                    return (isMass: false, isClass: true, isLate ? AttendanceStatus.Late : AttendanceStatus.Present, timeOfDay);
                }
            }

            // Mặc định cho điểm danh ngoài khung giờ quy ước
            return (isMass: false, isClass: false, AttendanceStatus.Present, timeOfDay);
        }
    }
}
