namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IBackgroundExportJobService
    {
        Task GenerateClassExcelReportJobAsync(string jobTrackerId, int classId, string requestedBy);

        Task GenerateParishFullExcelReportJobAsync(string jobTrackerId, int academicYearId, string? gradeLevel, string requestedBy);
    }

    public interface ILogMaintenanceJobService
    {
        void CleanUpOldLogsAndTempExports();
    }
}