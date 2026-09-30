using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStudentExcelService
    {
        byte[] GenerateTemplateFile();
        Task<StudentImportResultDto> ImportStudentsFromExcelAsync(Stream fileStream);
        Task<byte[]> ExportClassListToExcelAsync(int classId);
    }
}
