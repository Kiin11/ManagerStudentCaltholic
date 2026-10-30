namespace ManagerStudentCaltholic.Models.DTOs
{
    public class SacramentApprovalCandidateInputDto
    {
        public long StudentId { get; set; }
        public long EnrollmentId { get; set; }
        public bool IsSpecialExemption { get; set; }
        public bool IsApprovedForSacrament { get; set; }
        public string? GodparentName { get; set; }
        public string? AssignedRegistryNumber { get; set; }
        public string? EvaluationNote { get; set; }
    }
}
