namespace ManagerStudentCaltholic.Models.DTOs
{
    public class StudentCodeTransitionResultDto
    {
        public long StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string OldCode { get; set; } = string.Empty;
        public string SuffixedOldCode { get; set; } = string.Empty;
        public string FinalActiveCode { get; set; } = string.Empty;
        public string RolloverOutcome { get; set; } = string.Empty;
        public string? SourceClassName { get; set; }
        public string? TargetClassName { get; set; }
    }

    public class StudentRolloverCodeDecisionItem
    {
        public long StudentId { get; set; }
        public string RolloverOutcome { get; set; } = "PROMOTE"; // PROMOTE | RETAIN | GRADUATE | SKIP
        public int? TargetClassId { get; set; }
        public string? TargetClassName { get; set; }
    }
}
