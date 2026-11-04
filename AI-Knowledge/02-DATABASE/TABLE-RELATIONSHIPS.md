# TABLE RELATIONSHIPS

Quan hệ giữa các bảng được xác minh qua Fluent API cấu hình trong `ParishDbContext.cs` và các thuộc tính `[ForeignKey]` trên Entity Models.

## 1. Core Management (Thiếu nhi & Lớp học)
- **Enrollments ↔ Students (n-1):** Khóa ngoại `StudentId`. DeleteBehavior: `SetNull` (qua User) và ngầm định cascade qua bảng Enrollments (có SoftDelete chặn xoá vật lý).
- **Enrollments ↔ Classes (n-1):** Khóa ngoại `ClassRoomId`. `ClassRoom` chứa nhiều `Enrollment`. IsUnique composite index trên `(StudentId, ClassRoomId)`.
- **Classes ↔ AcademicYears (n-1):** Khóa ngoại `AcademicYearId` liên kết với niên khóa.
- **ClassTeachers ↔ Users (n-1):** Khóa ngoại `UserId`. DeleteBehavior: `SetNull`.
- **ClassTeachers ↔ Classes (n-1):** Khóa ngoại `ClassRoomId`. DeleteBehavior: `Cascade`.

## 2. Attendance & Grades (Điểm danh & Sổ điểm)
- **Attendances ↔ Enrollments (n-1):** Khóa ngoại `EnrollmentId`. DeleteBehavior ngầm định cascade.
- **AttendanceAuditLogs ↔ Attendances (n-1):** Khóa ngoại `AttendanceId`. DeleteBehavior: `Cascade`.
- **GradeRecords ↔ Enrollments (n-1):** Khóa ngoại `EnrollmentId`. DeleteBehavior: `Cascade`.
- **GradeRecords ↔ GradeConfigurations (n-1):** Khóa ngoại `GradeConfigurationId`. DeleteBehavior: `Cascade`.
- **AttendanceDisputeRequests ↔ Enrollments, Students, Classes, MassCampaigns (n-1):** Các khóa ngoại trỏ về thông tin buổi điểm danh bị khiếu nại.

## 3. Sacraments & Promotion (Bí tích & Kết chuyển)
- **StudentSacramentRecords ↔ Students (n-1):** Khóa ngoại `StudentId`. DeleteBehavior: `Cascade`.
- **SacramentApprovalBatches ↔ AcademicYears, Classes (n-1):** Khóa ngoại `AcademicYearId` (`Restrict`), `ClassRoomId` (`SetNull`).
- **SacramentApprovalItems ↔ SacramentApprovalBatches, Students, Enrollments (n-1):** DeleteBehavior: `Cascade`.
- **StudentCodeHistories ↔ Students, AcademicYears (n-1):** Khóa ngoại `StudentId` (`Cascade`), `SourceAcademicYearId` (`Restrict`).

## 4. Facilities & Operations (Cơ sở vật chất)
- **ClassRoomLocations ↔ BuildingZones (n-1):** Khóa ngoại `BuildingZoneId`. DeleteBehavior: `Cascade`.
- **ClassSchedules ↔ Classes, ClassRoomLocations (1-1/n-1):** Khóa ngoại `ClassRoomId` (`Cascade`), `ClassRoomLocationId` (`Restrict`).
- **RoomIncidentReports ↔ ClassRoomLocations, Users (n-1):** Khóa ngoại `ClassRoomLocationId`, `ReporterUserId`. DeleteBehavior: `Cascade`.

## 5. Security (Bảo mật)
- **RefreshTokens ↔ Users (n-1):** Khóa ngoại `UserId`. DeleteBehavior: `Cascade`.

```mermaid
erDiagram
    Students ||--o{ Enrollments : "1-N"
    Classes ||--o{ Enrollments : "1-N"
    AcademicYears ||--o{ Classes : "1-N"
    Users ||--o{ ClassTeachers : "1-N"
    Classes ||--o{ ClassTeachers : "1-N"
    Enrollments ||--o{ Attendances : "1-N"
    Attendances ||--o{ AttendanceAuditLogs : "1-N"
    Enrollments ||--o{ GradeRecords : "1-N"
    GradeConfigurations ||--o{ GradeRecords : "1-N"
    BuildingZones ||--o{ ClassRoomLocations : "1-N"
    ClassRoomLocations ||--o{ Classes : "1-N"
    Users ||--o{ RefreshTokens : "1-N"
	
	---

### 3. TABLE-INDEX.json
```json
{
  "database": "PostgreSQL",
  "schemas": [
    {
      "schemaName": "public",
      "tables": [
        {
          "tableName": "Students",
          "columns": ["Id", "StudentCode", "ChristianName", "FirstName", "LastName", "Gender", "DateOfBirth", "BaptismDate", "ConfirmationDate", "ParentPhone", "IsActive", "IsDeleted", "DeletedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [],
          "indexes": ["IsDeleted", "StudentCode"]
        },
        {
          "tableName": "AcademicYears",
          "columns": ["Id", "Name", "StartDate", "EndDate", "IsCurrent"],
          "primaryKeys": ["Id"],
          "foreignKeys": [],
          "indexes": []
        },
        {
          "tableName": "Classes",
          "columns": ["Id", "Name", "GradeLevel", "RoomName", "Shift", "AcademicYearId", "ClassRoomLocationId", "IsGradeLocked", "IsDeleted", "DeletedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "AcademicYearId", "principalTable": "AcademicYears", "principalColumn": "Id" },
            { "column": "ClassRoomLocationId", "principalTable": "ClassRoomLocations", "principalColumn": "Id" }
          ],
          "indexes": ["IsDeleted"]
        },
        {
          "tableName": "Enrollments",
          "columns": ["Id", "StudentId", "ClassRoomId", "EnrolledAt", "IsDeleted", "DeletedAt", "IsPassed", "EvaluationActionType", "TeacherProposalStatus", "TeacherProposalNote", "ProposedGodparentName", "ProposedBy", "ProposedAt", "AcademicYearId"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "StudentId", "principalTable": "Students", "principalColumn": "Id" },
            { "column": "ClassRoomId", "principalTable": "Classes", "principalColumn": "Id" }
          ],
          "indexes": ["StudentId, ClassRoomId", "IsDeleted"]
        },
        {
          "tableName": "Attendances",
          "columns": ["Id", "EnrollmentId", "AttendanceDate", "DayOfWeek", "AttendedMass", "MassStatus", "MassCheckInTime", "ClassAttended", "ClassStatus", "ClassCheckInTime", "IsMakeUp", "OriginalMissedDate", "Note", "CreatedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "EnrollmentId", "principalTable": "Enrollments", "principalColumn": "Id" }
          ],
          "indexes": ["EnrollmentId, AttendanceDate", "AttendanceDate", "DayOfWeek, AttendedMass", "DayOfWeek"]
        },
        {
          "tableName": "AttendanceAuditLogs",
          "columns": ["Id", "AttendanceId", "ActionType", "OldValues", "NewValues", "ModifiedBy", "Reason", "IpAddress", "CreatedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "AttendanceId", "principalTable": "Attendances", "principalColumn": "Id" }
          ],
          "indexes": ["AttendanceId", "CreatedAt", "ActionType"]
        },
        {
          "tableName": "ClassTeachers",
          "columns": ["Id", "ClassRoomId", "TeacherName", "UserId", "PhoneNumber", "RoleInClass", "AcademicYearId", "AssignedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "UserId", "principalTable": "Users", "principalColumn": "Id" },
            { "column": "ClassRoomId", "principalTable": "Classes", "principalColumn": "Id" },
            { "column": "AcademicYearId", "principalTable": "AcademicYears", "principalColumn": "Id" }
          ],
          "indexes": []
        },
        {
          "tableName": "Users",
          "columns": ["Id", "Username", "PasswordHash", "Email", "PhoneNumber", "FirstName", "LastName", "Role", "IsActive", "AccessFailedCount", "LockoutEnd", "CreatedAt", "LastLoginAt", "ChristianName", "DateOfBirth", "Address", "AvatarUrl", "ManagedGradeLevel", "ClassTeacherId", "StudentId"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "ClassTeacherId", "principalTable": "ClassTeachers", "principalColumn": "Id" },
            { "column": "StudentId", "principalTable": "Students", "principalColumn": "Id" }
          ],
          "indexes": ["Username", "Email", "PhoneNumber", "Role", "ManagedGradeLevel"]
        },
        {
          "tableName": "RefreshTokens",
          "columns": ["Id", "UserId", "Token", "ExpiresAt", "CreatedAt", "CreatedByIp", "RevokedAt", "RevokedByIp", "ReplacedByToken"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "UserId", "principalTable": "Users", "principalColumn": "Id" }
          ],
          "indexes": ["Token", "UserId"]
        },
        {
          "tableName": "StudentSacramentRecords",
          "columns": ["Id", "StudentId", "SacramentType", "ReceivedDate", "ParishName", "PriestName", "GodparentName", "RegistryBookNumber", "Notes", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsDeleted", "DeletedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "StudentId", "principalTable": "Students", "principalColumn": "Id" }
          ],
          "indexes": ["StudentId, SacramentType"]
        },
        {
          "tableName": "MassCampaigns",
          "columns": ["Id", "AcademicYearId", "ClassRoomId", "Title", "Description", "StartDate", "EndDate", "ApplicableDaysCsv", "TargetDaysCount", "Status", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsDeleted", "DeletedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "AcademicYearId", "principalTable": "AcademicYears", "principalColumn": "Id" },
            { "column": "ClassRoomId", "principalTable": "Classes", "principalColumn": "Id" }
          ],
          "indexes": ["AcademicYearId, ClassRoomId, Status", "IsDeleted"]
        },
        {
          "tableName": "MassCampaignRecords",
          "columns": ["Id", "CampaignId", "StudentId", "EnrollmentId", "AttendanceDate", "Status", "CheckInMethod", "CheckInTime", "Note", "LastModifiedBy", "LastModifiedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "CampaignId", "principalTable": "MassCampaigns", "principalColumn": "Id" },
            { "column": "StudentId", "principalTable": "Students", "principalColumn": "Id" },
            { "column": "EnrollmentId", "principalTable": "Enrollments", "principalColumn": "Id" }
          ],
          "indexes": ["CampaignId, EnrollmentId, AttendanceDate", "StudentId, AttendanceDate"]
        },
        {
          "tableName": "GradeConfigurations",
          "columns": ["Id", "AcademicYearId", "GradeLevel", "ClassRoomId", "Semester", "ScoreType", "DisplayName", "WeightFactor", "ColumnIndex", "IsSacramentExam", "IsRequired", "CreatedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "AcademicYearId", "principalTable": "AcademicYears", "principalColumn": "Id" },
            { "column": "ClassRoomId", "principalTable": "Classes", "principalColumn": "Id" }
          ],
          "indexes": ["AcademicYearId, GradeLevel, Semester"]
        },
        {
          "tableName": "GradeRecords",
          "columns": ["Id", "EnrollmentId", "GradeConfigurationId", "Score", "Note", "UpdatedBy", "UpdatedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "EnrollmentId", "principalTable": "Enrollments", "principalColumn": "Id" },
            { "column": "GradeConfigurationId", "principalTable": "GradeConfigurations", "principalColumn": "Id" }
          ],
          "indexes": ["EnrollmentId, GradeConfigurationId"]
        },
        {
          "tableName": "AttendanceDisputeRequests",
          "columns": ["Id", "EnrollmentId", "StudentId", "ClassRoomId", "CampaignId", "AttendanceDate", "SessionScope", "CurrentRecordedStatus", "RequestedStatus", "Reason", "ParentPhoneLast4", "Status", "TeacherResponseNote", "ReviewedBy", "ReviewedAt", "CreatedAt"],
          "primaryKeys": ["Id"],
          "foreignKeys": [
            { "column": "EnrollmentId", "principalTable": "Enrollments", "principalColumn": "Id" },
            { "column": "StudentId", "principalTable": "Students", "principalColumn": "Id" },
            { "column": "ClassRoomId", "principalTable": "Classes", "principalColumn": "Id" },
            { "column": "CampaignId", "principalTable": "MassCampaigns", "principalColumn": "Id" }
          ],
          "indexes": ["ClassRoomId, Status, CreatedAt", "EnrollmentId, AttendanceDate, SessionScope"]
        }
      ]
    }
  ]
}