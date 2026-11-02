using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IDeviceStudentRecognitionService
    {
        void RememberStudentOnDevice(HttpContext httpContext, Student student, ClassRoom? currentClass);

        RememberedStudentCookieItem? TryGetRememberedStudent(HttpContext httpContext, string studentCode);

        Task<DeviceDetectedStudentContextViewModel> DetectStudentsAndLoadAnnouncementsAsync(HttpContext httpContext);

        void DetachStudentFromDevice(HttpContext httpContext, string? studentCode = null);
    }
}