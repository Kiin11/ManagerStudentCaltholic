namespace ManagerStudentCaltholic.Services
{
    public static class AttendanceStatus
    {
        public const string LinedUp = "LINED_UP";
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
        public static (bool isMass, bool isClass, string calculatedStatus, TimeSpan timeOfDay, string message) EvaluateCheckIn(DateTime checkTime, string gradeLevel)
        {
            var dayOfWeek = checkTime.DayOfWeek;
            var time = checkTime.TimeOfDay;

            // 1. Thánh Lễ Thứ Năm (Khung giờ: 17:30 - 19:30)
            if (dayOfWeek == DayOfWeek.Thursday && time >= new TimeSpan(17, 30, 0) && time <= new TimeSpan(19, 30, 0))
            {
                return (true, false, AttendanceStatus.Present, time, "Tham dự Lễ Thứ Năm");
            }

            // 2. Thánh Lễ Chúa Nhật (Khung giờ: 06:20 - 08:20)
            if (dayOfWeek == DayOfWeek.Sunday && time >= new TimeSpan(6, 20, 0) && time <= new TimeSpan(8, 20, 0))
            {
                if (time < new TimeSpan(6, 35, 0))
                {
                    return (true, false, AttendanceStatus.LinedUp, time, "Đi Lễ: Có xếp hàng");
                }
                if (time < new TimeSpan(7, 0, 0))
                {
                    return (true, false, AttendanceStatus.Present, time, "Đi Lễ: Đúng giờ");
                }
                return (true, false, AttendanceStatus.Late, time, "Đi Lễ: Trễ");
            }

            // 3. Đi Học Khối Sáng (Khung giờ: 08:55 - 11:00)
            if (dayOfWeek == DayOfWeek.Sunday)
            {
                // Khối sáng (Khai tâm, Rước lễ): 09h00
                if ((gradeLevel == "Khai Tâm" || gradeLevel == "Rước Lễ" || gradeLevel == "Thêm Sức") &&
                    time >= new TimeSpan(8, 55, 0) && time <= new TimeSpan(11, 0, 0))
                {
                    if (time < new TimeSpan(9, 30, 0))
                    {
                        return (false, true, AttendanceStatus.Present, time, "Đi học đúng giờ");
                    }
                    return (false, true, AttendanceStatus.Late, time, "Đi học trễ");
                }

                // Khối chiều (Bao đồng): 15h00
                if ((gradeLevel == "Bao Đồng") &&
                    time >= new TimeSpan(14, 30, 0) && time <= new TimeSpan(17, 10, 0))
                {
                    if (time < new TimeSpan(15, 15, 0))
                    {
                        return (false, true, AttendanceStatus.Present, time, "Đi học đúng giờ");
                    }
                    return (false, true, AttendanceStatus.Late, time, "Đi học trễ");
                }
            }

            // Mặc định cho điểm danh ngoài khung giờ quy ước
            return (isMass: false, isClass: false, AttendanceStatus.Present, time, "Ngoài khung giờ điểm danh");
        }
    }
}
