using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface ILessonPlanImportService
    {
        Task<List<ClassLessonPlan>> ParseFileAsync(IFormFile file, int classRoomId, int academicYearId);
    }
}
