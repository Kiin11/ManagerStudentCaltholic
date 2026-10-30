using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Enum;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class SacramentRegistryService : ISacramentRegistryService
    {
        private readonly ParishDbContext _context;

        // Định nghĩa 4 mốc Bí tích chuẩn và độ tuổi khuyến nghị
        private static readonly (int Type, string Name, int StandardAge)[] SacramentDefinitions =
        {
            ((int)SacramentTypeEnum.Baptism, "Bí tích Rửa Tội", 1),
            ((int)SacramentTypeEnum.FirstCommunion, "Xưng Tội & Rước Lễ Lần Đầu", 10),
            ((int)SacramentTypeEnum.Confirmation, "Bí tích Thêm Sức", 13),
            ((int)SacramentTypeEnum.ProfessionOfFaith, "Bao Đồng (Tuyên Hứa Sống Đạo)", 16)
        };

        public SacramentRegistryService(ParishDbContext context)
        {
            _context = context;
        }

        public async Task<List<SacramentRecordItemDto>> GetStudentSacramentsAsync(long studentId)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId);

            var records = await _context.StudentSacramentRecords
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderBy(r => r.SacramentType)
                .ToListAsync();

            var recordDict = records.ToDictionary(r => r.SacramentType, r => r);
            var timeline = new List<SacramentRecordItemDto>();

            foreach (var def in SacramentDefinitions)
            {
                if (recordDict.TryGetValue(def.Type, out var rec))
                {
                    timeline.Add(new SacramentRecordItemDto
                    {
                        Id = rec.Id,
                        StudentId = studentId,
                        SacramentType = def.Type,
                        SacramentName = def.Name,
                        IsCompleted = true,
                        ReceivedDate = rec.ReceivedDate.ToString("yyyy-MM-dd"),
                        ReceivedDateDisplay = rec.ReceivedDate.ToString("dd/MM/yyyy"),
                        ParishName = rec.ParishName,
                        PriestName = rec.PriestName,
                        GodparentName = rec.GodparentName,
                        RegistryBookNumber = rec.RegistryBookNumber,
                        Notes = rec.Notes,
                        UpdatedBy = rec.UpdatedBy ?? rec.CreatedBy ?? "Hệ thống",
                        UpdatedAtDisplay = (rec.UpdatedAt ?? rec.CreatedAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    });
                }
                else if (student != null && def.Type == (int)SacramentTypeEnum.Baptism && student.BaptismDate.HasValue)
                {
                    // Fallback hiển thị ngày Rửa tội từ bảng Students nếu chưa nhập Sổ bộ chi tiết
                    timeline.Add(new SacramentRecordItemDto
                    {
                        Id = 0,
                        StudentId = studentId,
                        SacramentType = def.Type,
                        SacramentName = def.Name,
                        IsCompleted = true,
                        ReceivedDate = student.BaptismDate.Value.ToString("yyyy-MM-dd"),
                        ReceivedDateDisplay = student.BaptismDate.Value.ToString("dd/MM/yyyy"),
                        ParishName = "Giáo xứ Phú Trung (Kế thừa hồ sơ gốc)",
                        Notes = "Chưa cập nhật Số sổ bộ & Linh mục ban Bí tích"
                    });
                }
                else if (student != null && def.Type == (int)SacramentTypeEnum.Confirmation && student.ConfirmationDate.HasValue)
                {
                    // Fallback hiển thị ngày Thêm sức từ bảng Students nếu chưa nhập Sổ bộ chi tiết
                    timeline.Add(new SacramentRecordItemDto
                    {
                        Id = 0,
                        StudentId = studentId,
                        SacramentType = def.Type,
                        SacramentName = def.Name,
                        IsCompleted = true,
                        ReceivedDate = student.ConfirmationDate.Value.ToString("yyyy-MM-dd"),
                        ReceivedDateDisplay = student.ConfirmationDate.Value.ToString("dd/MM/yyyy"),
                        ParishName = "Giáo xứ Phú Trung (Kế thừa hồ sơ gốc)",
                        Notes = "Chưa cập nhật Số sổ bộ & Linh mục ban Bí tích"
                    });
                }
                else
                {
                    timeline.Add(new SacramentRecordItemDto
                    {
                        Id = 0,
                        StudentId = studentId,
                        SacramentType = def.Type,
                        SacramentName = def.Name,
                        IsCompleted = false
                    });
                }
            }

            return timeline;
        }

        public async Task<bool> DeleteSacramentRecordAsync(long recordId, long studentId, string operatorName)
        {
            var record = await _context.StudentSacramentRecords
                .FirstOrDefaultAsync(r => r.Id == recordId && r.StudentId == studentId);

            if (record == null) return false;

            int sacType = record.SacramentType;
            record.UpdatedBy = operatorName;
            record.UpdatedAt = DateTime.UtcNow;

            // ParishDbContext.SaveChangesAsync tự động chuyển Remove thành Soft Delete (IsDeleted = true)
            _context.StudentSacramentRecords.Remove(record);

            // Đồng bộ xóa ngày tương ứng trên bảng Students nếu xóa Bí tích Rửa tội hoặc Thêm sức
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student != null)
            {
                if (sacType == (int)SacramentTypeEnum.Baptism)
                {
                    student.BaptismDate = null;
                }
                else if (sacType == (int)SacramentTypeEnum.Confirmation)
                {
                    student.ConfirmationDate = null;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MissingSacramentAlertDto>> GetMissingSacramentsAsync(long studentId, int currentAge)
        {
            var timeline = await GetStudentSacramentsAsync(studentId);
            var alerts = new List<MissingSacramentAlertDto>();

            foreach (var def in SacramentDefinitions)
            {
                var step = timeline.FirstOrDefault(t => t.SacramentType == def.Type);
                bool completed = step?.IsCompleted ?? false;

                if (!completed && currentAge >= def.StandardAge)
                {
                    alerts.Add(new MissingSacramentAlertDto
                    {
                        SacramentType = def.Type,
                        SacramentName = def.Name,
                        StandardAge = def.StandardAge,
                        CurrentAge = currentAge,
                        WarningMessage = $"Thiếu nhi đã {currentAge} tuổi (độ tuổi chuẩn: >= {def.StandardAge} tuổi) nhưng chưa có hồ sơ lãnh nhận {def.Name}."
                    });
                }
            }

            return alerts;
        }

        public async Task<StudentSacramentRecord> AddOrUpdateSacramentRecordAsync(SaveSacramentRecordRequestDto dto, string operatorName)
        {
            var receivedUtc = DateTime.SpecifyKind(dto.ReceivedDate.Date, DateTimeKind.Utc);
            var safeOperator = string.IsNullOrWhiteSpace(operatorName) ? "Staff" : operatorName.Trim();

            var existing = await _context.StudentSacramentRecords
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.StudentId == dto.StudentId && r.SacramentType == dto.SacramentType);

            if (existing == null)
            {
                existing = new StudentSacramentRecord
                {
                    StudentId = dto.StudentId,
                    SacramentType = dto.SacramentType,
                    ReceivedDate = receivedUtc,
                    ParishName = dto.ParishName.Trim(),
                    PriestName = string.IsNullOrWhiteSpace(dto.PriestName) ? string.Empty : dto.PriestName.Trim(),
                    GodparentName = string.IsNullOrWhiteSpace(dto.GodparentName) ? string.Empty : dto.GodparentName.Trim(),
                    RegistryBookNumber = string.IsNullOrWhiteSpace(dto.RegistryBookNumber) ? string.Empty : dto.RegistryBookNumber.Trim(),
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? string.Empty : dto.Notes.Trim(),
                    CreatedBy = safeOperator,
                    UpdatedBy = safeOperator,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    DeletedAt = null
                };
                _context.StudentSacramentRecords.Add(existing);
            }
            else
            {
                existing.ReceivedDate = receivedUtc;
                existing.ParishName = dto.ParishName.Trim();
                existing.PriestName = string.IsNullOrWhiteSpace(dto.PriestName) ? string.Empty : dto.PriestName.Trim();
                existing.GodparentName = string.IsNullOrWhiteSpace(dto.GodparentName) ? string.Empty : dto.GodparentName.Trim();
                existing.RegistryBookNumber = string.IsNullOrWhiteSpace(dto.RegistryBookNumber) ? string.Empty : dto.RegistryBookNumber.Trim();
                existing.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? string.Empty : dto.Notes.Trim();
                existing.UpdatedBy = safeOperator;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

            // Đồng bộ ngày Rửa Tội (1) và Thêm Sức (3) ngược về bảng Students
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == dto.StudentId);
            if (student != null)
            {
                if (dto.SacramentType == (int)SacramentTypeEnum.Baptism)
                {
                    student.BaptismDate = receivedUtc;
                }
                else if (dto.SacramentType == (int)SacramentTypeEnum.Confirmation)
                {
                    student.ConfirmationDate = receivedUtc;
                }
            }

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}