namespace ManagerStudentCaltholic.Models.DTOs
{
    public class ClassAttendanceRuleDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int ThursdayMassWeightPercent { get; set; } = 20;
        public int SundayMassWeightPercent { get; set; } = 40;
        public int ClassWeightPercent { get; set; } = 40;
        public decimal LateMultiplier { get; set; } = 0.80m;
        public decimal PermittedAbsentMultiplier { get; set; } = 0.50m;
        public decimal MakeUpBonusRate { get; set; } = 1.00m;
        public double MinAttendanceRateForSacrament { get; set; } = 80.0;
        public bool CanEdit { get; set; }
    }
}