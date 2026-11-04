# DATABASE OVERVIEW & CODE-TO-DATABASE MAPPING

## A. Database Architecture
Hệ thống sử dụng cơ sở dữ liệu quan hệ **PostgreSQL** kết hợp với Object-Relational Mapper (ORM) là **Entity Framework Core 8** (`Npgsql.EntityFrameworkCore.PostgreSQL`). 
- Phương pháp tiếp cận: Code-First Migrations.
- Global Query Filters: Được áp dụng để tự động loại bỏ các bản ghi đã xóa mềm (Soft Delete) có cờ `IsDeleted = true`.
- Data Protection/Concurrency: Sử dụng Database Transactions (`BeginTransactionAsync`) và Execution Strategy để đảm bảo tính toàn vẹn dữ liệu khi thực thi các tác vụ hàng loạt (Batch Upsert, Promotion Rollover).

## B. Code-to-Database Mapping (Xác nhận từ ORM)
Mối liên hệ giữa C# Code và Database được xác nhận thông qua cấu hình `ParishDbContext.cs`.

| C# Entity Class (Model) | DbSet Property | Database Table | Mapping Type | Evidence |
|---|---|---|---|---|
| `Student` | `Students` | `Students` | ORM Confirmed | `ParishDbContext.cs` |
| `AcademicYear` | `AcademicYears` | `AcademicYears` | ORM Confirmed | `ParishDbContext.cs` |
| `ClassRoom` | `Classes` | `Classes` | ORM Confirmed | `ParishDbContext.cs` |
| `Enrollment` | `Enrollments` | `Enrollments` | ORM Confirmed | `ParishDbContext.cs` |
| `Attendance` | `Attendances` | `Attendances` | ORM Confirmed | `ParishDbContext.cs` |
| `AttendanceAuditLog` | `AttendanceAuditLogs` | `AttendanceAuditLogs` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("AttendanceAuditLogs")`) |
| `ClassTeacher` | `ClassTeachers` | `ClassTeachers` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("ClassTeachers")`) |
| `User` | `Users` | `Users` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("Users")`) |
| `RefreshToken` | `RefreshTokens` | `RefreshTokens` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("RefreshTokens")`) |
| `Announcement` | `Announcements` | `Announcements` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("Announcements")`) |
| `BuildingZone` | `BuildingZones` | `BuildingZones` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("BuildingZones")`) |
| `ClassRoomLocation` | `ClassRoomLocations` | `ClassRoomLocations` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("ClassRoomLocations")`) |
| `RoomIncidentReport` | `RoomIncidentReports` | `RoomIncidentReports` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("RoomIncidentReports")`) |
| `ClassSchedule` | `ClassSchedules` | `ClassSchedules` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("ClassSchedules")`) |
| `BranchHeadAssignment` | `BranchHeadAssignments` | `BranchHeadAssignments` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("BranchHeadAssignments")`) |
| `UserRoleHistory` | `UserRoleHistories` | `UserRoleHistory` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("UserRoleHistory")`) |
| `ClassLessonDocument` | `ClassLessonDocuments` | `ClassLessonDocuments` | ORM Confirmed | `ParishDbContext.cs` |
| `GradeConfiguration` | `GradeConfigurations` | `GradeConfigurations` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("GradeConfigurations")`) |
| `GradeRecord` | `GradeRecords` | `GradeRecords` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("GradeRecords")`) |
| `AbsenceRequest` | `AbsenceRequests` | `AbsenceRequests` | ORM Confirmed | `ParishDbContext.cs` |
| `AttendanceRuleConfig` | `AttendanceRuleConfigs` | `AttendanceRuleConfigs` | ORM Confirmed | `ParishDbContext.cs` |
| `GradeColumn` | `GradeColumns` | `grade_columns` | ORM Confirmed | `GradeColumn.cs` (`[Table("grade_columns")]`) |
| `StudentGrade` | `StudentGrades` | `student_grades` | ORM Confirmed | `StudentGrade.cs` (`[Table("student_grades")]`) |
| `AttendanceScoreRule` | `AttendanceScoreRules` | `attendance_score_rules` | ORM Confirmed | `AttendanceScoreRule.cs` (`[Table("attendance_score_rules")]`) |
| `GradingFormula` | `GradingFormulas` | `grading_formulas` | ORM Confirmed | `GradingFormula.cs` (`[Table("grading_formulas")]`) |
| `StudentGradeSummary` | `StudentGradeSummaries` | `student_grade_summaries` | ORM Confirmed | `GradingFormula.cs` (`[Table("student_grade_summaries")]`) |
| `TeacherClass` | `TeacherClasses` | `teacher_classes` | ORM Confirmed | `TeacherClass.cs` (`[Table("teacher_classes")]`) |
| `ParentMessageTemplate` | `ParentMessageTemplates` | `parent_message_templates` | ORM Confirmed | `ParentMessageTemplate.cs` (`[Table("parent_message_templates")]`) |
| `StudentRegistrationRequest` | `StudentRegistrationRequests` | `student_registration_requests`| ORM Confirmed | `StudentRegistrationRequest.cs` (`[Table("student_registration_requests")]`) |
| `PromotionRuleConfig` | `PromotionRuleConfigs` | `PromotionRuleConfigs` | ORM Confirmed | `ParishDbContext.cs` |
| `StudentSacramentRecord` | `StudentSacramentRecords` | `StudentSacramentRecords` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("StudentSacramentRecords")`) |
| `SacramentApprovalBatch` | `SacramentApprovalBatches` | `SacramentApprovalBatches`| ORM Confirmed | `ParishDbContext.cs` (`ToTable("SacramentApprovalBatches")`) |
| `SacramentApprovalItem` | `SacramentApprovalItems` | `SacramentApprovalItems` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("SacramentApprovalItems")`) |
| `MassCampaign` | `MassCampaigns` | `MassCampaigns` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("MassCampaigns")`) |
| `MassCampaignRecord` | `MassCampaignRecords` | `MassCampaignRecords` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("MassCampaignRecords")`) |
| `StudentCodeHistory` | `StudentCodeHistories` | `StudentCodeHistories` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("StudentCodeHistories")`) |
| `AttendanceDisputeRequest` | `AttendanceDisputeRequests` | `AttendanceDisputeRequests` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("AttendanceDisputeRequests")`) |
| `FeaturePermissionRule` | `FeaturePermissionRules` | `FeaturePermissionRules` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("FeaturePermissionRules")`) |
| `StaffQuickLookupLog` | `StaffQuickLookupLogs` | `StaffQuickLookupLogs` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("StaffQuickLookupLogs")`), `StaffQuickLookupLog.cs` (`[Table("StaffQuickLookupLogs")]`) |
| `LookupViewOption` | `LookupViewOptions` | `LookupViewOptions` | ORM Confirmed | `ParishDbContext.cs` (`ToTable("LookupViewOptions")`), `LookupViewOption.cs` (`[Table("LookupViewOptions")]`) |

## C. Database Context Configuration
- Tất cả cấu hình Foreign Key, Index, Unique Constraint, Seed Data (`HasData`) và Default Values được khai báo theo chuẩn Fluent API trong phương thức `OnModelCreating` của lớp `ParishDbContext`.
