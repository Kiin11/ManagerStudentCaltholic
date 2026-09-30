namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStudentCodeGenerator
    {
        Task<string> GenerateUniqueCodeAsync();
    }
}
