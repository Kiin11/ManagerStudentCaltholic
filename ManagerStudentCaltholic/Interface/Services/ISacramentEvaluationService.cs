using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface ISacramentEvaluationService
    {
        string GetSacramentDisplayName(int sacramentType);

        int InferDefaultSacramentTypeByGrade(string gradeLevel);

        Task<SacramentEvaluationFilterViewModel> EvaluateCandidatesAsync(
            int academicYearId,
            string gradeLevel,
            int? classId,
            int sacramentType,
            decimal minYearAvg = 5.0m,
            double minAttendanceRate = 80.0,
            bool requireSacramentExam = true,
            decimal minSacramentScore = 5.0m,
            long? batchId = null);

        Task<SacramentApprovalBatch> SaveDraftBatchAsync(
            SaveSacramentApprovalBatchRequestDto dto,
            string operatorName);

        Task<(bool Success, int SyncedCount, string Message)> ApproveBatchAndSyncToRegistryAsync(
            long batchId,
            string approverName);

        Task<byte[]> ExportApprovalSheetToExcelAsync(
            int academicYearId,
            string gradeLevel,
            int? classId,
            int sacramentType,
            decimal minYearAvg,
            double minAttendanceRate,
            bool requireSacramentExam,
            decimal minSacramentScore,
            long? batchId);
    }
}