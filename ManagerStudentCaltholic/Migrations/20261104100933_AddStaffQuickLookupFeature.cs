using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ManagerStudentCaltholic.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffQuickLookupFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_score_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: true),
                    base_score = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_mass_unexcused = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_mass_excused = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_mass_late = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_class_unexcused = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_class_excused = table.Column<decimal>(type: "numeric", nullable: false),
                    penalty_class_late = table.Column<decimal>(type: "numeric", nullable: false),
                    min_score = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_score_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZoneName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZoneCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeaturePermissionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeatureCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MasterCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    IconClass = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BadgeColor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AllowedRolesCsv = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SacramentClassesOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SpecificClassIdsCsv = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturePermissionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grade_columns",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: true),
                    column_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    column_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coefficient = table.Column<decimal>(type: "numeric", nullable: false),
                    is_attendance_based = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grade_columns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grading_formulas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: true),
                    formula_expression = table.Column<string>(type: "text", nullable: false),
                    evaluation_rules = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_formulas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LookupViewOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IconClass = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "bi-card-list"),
                    BadgeColor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "primary"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupViewOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parent_message_templates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content_template = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_message_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_grade_summaries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    average_score = table.Column<decimal>(type: "numeric", nullable: true),
                    classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_passed = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_grade_summaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChristianName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: false),
                    LastName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    BaptismDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ParentPhone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teacher_classes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: false),
                    role_in_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRuleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MinYearAverage = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    MinAttendanceRate = table.Column<double>(type: "double precision", nullable: false),
                    RequireSacramentPassed = table.Column<bool>(type: "boolean", nullable: false),
                    MinSacramentScore = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    NextGradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRuleConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionRuleConfigs_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassRoomLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingZoneId = table.Column<int>(type: "integer", nullable: false),
                    RoomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FloorNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 40),
                    EquipmentNotes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassRoomLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassRoomLocations_BuildingZones_BuildingZoneId",
                        column: x => x.BuildingZoneId,
                        principalTable: "BuildingZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_grades",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    grade_column_id = table.Column<int>(type: "integer", nullable: false),
                    semester = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_grades", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_grades_grade_columns_grade_column_id",
                        column: x => x.grade_column_id,
                        principalTable: "grade_columns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentCodeHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    SourceAcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    TargetAcademicYearId = table.Column<int>(type: "integer", nullable: true),
                    OriginalCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArchivedYearCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NewCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RolloverOutcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCodeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCodeHistories_AcademicYears_SourceAcademicYearId",
                        column: x => x.SourceAcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCodeHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentSacramentRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    SacramentType = table.Column<int>(type: "integer", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ParishName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PriestName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GodparentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RegistryBookNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSacramentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSacramentRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RoomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    ClassRoomLocationId = table.Column<int>(type: "integer", nullable: true),
                    IsGradeLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classes_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Classes_ClassRoomLocations_ClassRoomLocationId",
                        column: x => x.ClassRoomLocationId,
                        principalTable: "ClassRoomLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ALL"),
                    TargetGradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TargetClassRoomId = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NORMAL"),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    ExpireAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Classes_TargetClassRoomId",
                        column: x => x.TargetClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRuleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    SundayMassWeightPercent = table.Column<int>(type: "integer", nullable: false),
                    ThursdayMassWeightPercent = table.Column<int>(type: "integer", nullable: false),
                    ClassWeightPercent = table.Column<int>(type: "integer", nullable: false),
                    LateMultiplier = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    PermittedAbsentMultiplier = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    MakeUpBonusRate = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    MinAttendanceRateForSacrament = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRuleConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRuleConfigs_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassLessonDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLessonDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassLessonDocuments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassLessonDocuments_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    ClassRoomLocationId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Notes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_ClassRoomLocations_ClassRoomLocationId",
                        column: x => x.ClassRoomLocationId,
                        principalTable: "ClassRoomLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSchedules_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluationActionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TeacherProposalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TeacherProposalNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProposedGodparentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProposedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProposedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradeConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: true),
                    Semester = table.Column<int>(type: "integer", nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeightFactor = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false),
                    IsSacramentExam = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeConfigurations_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeConfigurations_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MassCampaigns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApplicableDaysCsv = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetDaysCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MassCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MassCampaigns_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MassCampaigns_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SacramentApprovalBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: true),
                    SacramentType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinYearAverage = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    MinAttendanceRate = table.Column<double>(type: "double precision", nullable: false),
                    RequireSacramentExam = table.Column<bool>(type: "boolean", nullable: false),
                    MinSacramentExamScore = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    PlannedCeremonyDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CeremonyParish = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CelebrantPriest = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RegistryBookPrefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SacramentApprovalBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SacramentApprovalBatches_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SacramentApprovalBatches_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_registration_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    christian_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    baptism_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    baptism_parish = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parent_full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    parent_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    parish_sub_division = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registration_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    academic_year_id = table.Column<int>(type: "integer", nullable: true),
                    target_grade_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    baptism_certificate_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_student_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_class_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_registration_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_registration_requests_AcademicYears_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_registration_requests_Classes_assigned_class_id",
                        column: x => x.assigned_class_id,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_student_registration_requests_Students_created_student_id",
                        column: x => x.created_student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AbsenceRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    AbsenceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SessionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsenceRequests_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    AttendedMass = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MassStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ABSENT_UNPERMITTED"),
                    MassCheckInTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ClassAttended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClassStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ABSENT_UNPERMITTED"),
                    ClassCheckInTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsMakeUp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OriginalMissedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradeRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    GradeConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeRecords_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeRecords_GradeConfigurations_GradeConfigurationId",
                        column: x => x.GradeConfigurationId,
                        principalTable: "GradeConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDisputeRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    CampaignId = table.Column<long>(type: "bigint", nullable: true),
                    AttendanceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SessionScope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentRecordedStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentPhoneLast4 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TeacherResponseNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDisputeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputeRequests_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputeRequests_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputeRequests_MassCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MassCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputeRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MassCampaignRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckInMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MassCampaignRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MassCampaignRecords_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MassCampaignRecords_MassCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MassCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MassCampaignRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SacramentApprovalItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    EnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Semester1Average = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Semester2Average = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    YearAverage = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    SacramentExamScore = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    MassAttendanceRate = table.Column<double>(type: "double precision", nullable: false),
                    ClassAttendanceRate = table.Column<double>(type: "double precision", nullable: false),
                    CombinedAttendanceRate = table.Column<double>(type: "double precision", nullable: false),
                    IsSystemEligible = table.Column<bool>(type: "boolean", nullable: false),
                    IsSpecialExemption = table.Column<bool>(type: "boolean", nullable: false),
                    IsApprovedForSacrament = table.Column<bool>(type: "boolean", nullable: false),
                    GodparentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AssignedRegistryNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EvaluationNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SacramentApprovalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SacramentApprovalItems_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SacramentApprovalItems_SacramentApprovalBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "SacramentApprovalBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SacramentApprovalItems_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "SYSTEM"),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceAuditLogs_Attendances_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchHeadAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    ManagedGradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    Notes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchHeadAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchHeadAssignments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassLessonPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    LessonDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LiturgicalDay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LessonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TopicTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    KeyPoints = table.Column<string>(type: "text", nullable: true),
                    AssignedTeacherUserId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedTeacherName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsExamDay = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDayOff = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLessonPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassLessonPlans_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassLessonPlans_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassTeachers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomId = table.Column<int>(type: "integer", nullable: false),
                    TeacherName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    RoleInClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "HEAD"),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTeachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassTeachers_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassTeachers_Classes_ClassRoomId",
                        column: x => x.ClassRoomId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Teacher"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ChristianName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ManagedGradeLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassTeacherId = table.Column<long>(type: "bigint", nullable: true),
                    StudentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_ClassTeachers_ClassTeacherId",
                        column: x => x.ClassTeacherId,
                        principalTable: "ClassTeachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    CreatedByIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedByIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ReplacedByToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomIncidentReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassRoomLocationId = table.Column<int>(type: "integer", nullable: false),
                    ReporterUserId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NORMAL"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    ReportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomIncidentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomIncidentReports_ClassRoomLocations_ClassRoomLocationId",
                        column: x => x.ClassRoomLocationId,
                        principalTable: "ClassRoomLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomIncidentReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffQuickLookupLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    SearchTerm = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SelectedOption = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OVERVIEW"),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LookedUpAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffQuickLookupLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffQuickLookupLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffQuickLookupLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    OldRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NewRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AcademicYearId = table.Column<int>(type: "integer", nullable: true),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoleHistory_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserRoleHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserRoleHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LookupViewOptions",
                columns: new[] { "Id", "BadgeColor", "Description", "DisplayName", "IconClass", "IsActive", "OptionCode", "SortOrder" },
                values: new object[,]
                {
                    { 1, "primary", "Hồ sơ cá nhân, phân bổ lớp học & tóm tắt tình trạng hiện tại", "Tổng quan", "bi-person-badge", true, "OVERVIEW", 1 },
                    { 2, "success", "Lễ Chúa Nhật & Thứ Năm gần nhất, giờ học & Chiến dịch đi lễ 1 tháng", "Chuyên cần & Chiến dịch", "bi-calendar-check", true, "ATTENDANCE_DETAIL", 2 },
                    { 3, "warning", "ĐTB cả năm, tỷ lệ chuyên cần 2 năm trước, bảng điểm & mốc Bí tích", "Lịch sử 2 năm & Bí tích", "bi-journal-bookmark", true, "ACADEMIC_2Y_SACRAMENT", 3 },
                    { 4, "info", "Thông tin liên hệ phụ huynh, địa chỉ & mẫu soạn tin nhắn Zalo/SMS", "Liên hệ phụ huynh", "bi-telephone", true, "PARENT_CONTACT", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRequests_EnrollmentId",
                table: "AbsenceRequests",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedAt",
                table: "Announcements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_IsPinned",
                table: "Announcements",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Scope",
                table: "Announcements",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetClassRoomId",
                table: "Announcements",
                column: "TargetClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetGradeLevel",
                table: "Announcements",
                column: "TargetGradeLevel");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_score_rules_academic_year_semester",
                table: "attendance_score_rules",
                columns: new[] { "academic_year", "semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAuditLogs_ActionType",
                table: "AttendanceAuditLogs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAuditLogs_AttendanceId",
                table: "AttendanceAuditLogs",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAuditLogs_CreatedAt",
                table: "AttendanceAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputeRequests_CampaignId",
                table: "AttendanceDisputeRequests",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputeRequests_ClassRoomId_Status_CreatedAt",
                table: "AttendanceDisputeRequests",
                columns: new[] { "ClassRoomId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputeRequests_EnrollmentId_AttendanceDate_Sessi~",
                table: "AttendanceDisputeRequests",
                columns: new[] { "EnrollmentId", "AttendanceDate", "SessionScope" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputeRequests_StudentId",
                table: "AttendanceDisputeRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRuleConfigs_ClassRoomId",
                table: "AttendanceRuleConfigs",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_AttendanceDate",
                table: "Attendances",
                column: "AttendanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_DayOfWeek",
                table: "Attendances",
                column: "DayOfWeek");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_DayOfWeek_AttendedMass",
                table: "Attendances",
                columns: new[] { "DayOfWeek", "AttendedMass" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EnrollmentId_AttendanceDate",
                table: "Attendances",
                columns: new[] { "EnrollmentId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchHeadAssignments_AcademicYearId_ManagedGradeLevel",
                table: "BranchHeadAssignments",
                columns: new[] { "AcademicYearId", "ManagedGradeLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchHeadAssignments_AssignedByUserId",
                table: "BranchHeadAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchHeadAssignments_UserId",
                table: "BranchHeadAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_AcademicYearId",
                table: "Classes",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_ClassRoomLocationId",
                table: "Classes",
                column: "ClassRoomLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_IsDeleted",
                table: "Classes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonDocuments_AcademicYearId",
                table: "ClassLessonDocuments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonDocuments_ClassRoomId",
                table: "ClassLessonDocuments",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonPlans_AcademicYearId",
                table: "ClassLessonPlans",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonPlans_AssignedTeacherUserId",
                table: "ClassLessonPlans",
                column: "AssignedTeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonPlans_ClassRoomId_LessonDate",
                table: "ClassLessonPlans",
                columns: new[] { "ClassRoomId", "LessonDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoomLocations_BuildingZoneId_FloorNumber",
                table: "ClassRoomLocations",
                columns: new[] { "BuildingZoneId", "FloorNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_ClassRoomId",
                table: "ClassSchedules",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_ClassRoomLocationId_DayOfWeek_StartTime",
                table: "ClassSchedules",
                columns: new[] { "ClassRoomLocationId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeachers_AcademicYearId",
                table: "ClassTeachers",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeachers_ClassRoomId",
                table: "ClassTeachers",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeachers_UserId",
                table: "ClassTeachers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassRoomId",
                table: "Enrollments",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_IsDeleted",
                table: "Enrollments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_AcademicYearId",
                table: "Enrollments",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_ClassRoomId",
                table: "Enrollments",
                columns: new[] { "StudentId", "ClassRoomId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePermissionRules_FeatureCode",
                table: "FeaturePermissionRules",
                column: "FeatureCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePermissionRules_MasterCode_IsEnabled_SortOrder",
                table: "FeaturePermissionRules",
                columns: new[] { "MasterCode", "IsEnabled", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_grade_columns_academic_year_semester_class_id_column_code",
                table: "grade_columns",
                columns: new[] { "academic_year", "semester", "class_id", "column_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeConfigurations_AcademicYearId_GradeLevel_Semester",
                table: "GradeConfigurations",
                columns: new[] { "AcademicYearId", "GradeLevel", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeConfigurations_ClassRoomId",
                table: "GradeConfigurations",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeRecords_EnrollmentId_GradeConfigurationId",
                table: "GradeRecords",
                columns: new[] { "EnrollmentId", "GradeConfigurationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeRecords_GradeConfigurationId",
                table: "GradeRecords",
                column: "GradeConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_grading_formulas_academic_year_semester",
                table: "grading_formulas",
                columns: new[] { "academic_year", "semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LookupViewOptions_OptionCode",
                table: "LookupViewOptions",
                column: "OptionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaignRecords_CampaignId_EnrollmentId_AttendanceDate",
                table: "MassCampaignRecords",
                columns: new[] { "CampaignId", "EnrollmentId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaignRecords_EnrollmentId",
                table: "MassCampaignRecords",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaignRecords_StudentId_AttendanceDate",
                table: "MassCampaignRecords",
                columns: new[] { "StudentId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaigns_AcademicYearId_ClassRoomId_Status",
                table: "MassCampaigns",
                columns: new[] { "AcademicYearId", "ClassRoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaigns_ClassRoomId",
                table: "MassCampaigns",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MassCampaigns_IsDeleted",
                table: "MassCampaigns",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRuleConfigs_AcademicYearId_GradeLevel",
                table: "PromotionRuleConfigs",
                columns: new[] { "AcademicYearId", "GradeLevel" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomIncidentReports_ClassRoomLocationId_Status",
                table: "RoomIncidentReports",
                columns: new[] { "ClassRoomLocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomIncidentReports_ReporterUserId",
                table: "RoomIncidentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalBatches_AcademicYearId_GradeLevel_Sacramen~",
                table: "SacramentApprovalBatches",
                columns: new[] { "AcademicYearId", "GradeLevel", "SacramentType" });

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalBatches_ClassRoomId",
                table: "SacramentApprovalBatches",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalBatches_IsDeleted",
                table: "SacramentApprovalBatches",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalBatches_Status",
                table: "SacramentApprovalBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalItems_BatchId_StudentId",
                table: "SacramentApprovalItems",
                columns: new[] { "BatchId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalItems_EnrollmentId",
                table: "SacramentApprovalItems",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SacramentApprovalItems_StudentId",
                table: "SacramentApprovalItems",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffQuickLookupLogs_StudentId",
                table: "StaffQuickLookupLogs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffQuickLookupLogs_UserId_LookedUpAt",
                table: "StaffQuickLookupLogs",
                columns: new[] { "UserId", "LookedUpAt" });

            migrationBuilder.CreateIndex(
                name: "IX_student_grade_summaries_student_id_academic_year_semester",
                table: "student_grade_summaries",
                columns: new[] { "student_id", "academic_year", "semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_grades_grade_column_id",
                table: "student_grades",
                column: "grade_column_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_grades_student_id_grade_column_id_semester",
                table: "student_grades",
                columns: new[] { "student_id", "grade_column_id", "semester" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_registration_requests_academic_year_id",
                table: "student_registration_requests",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_registration_requests_assigned_class_id",
                table: "student_registration_requests",
                column: "assigned_class_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_registration_requests_created_student_id",
                table: "student_registration_requests",
                column: "created_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_registration_requests_parent_phone_date_of_birth",
                table: "student_registration_requests",
                columns: new[] { "parent_phone", "date_of_birth" });

            migrationBuilder.CreateIndex(
                name: "IX_student_registration_requests_status_created_at",
                table: "student_registration_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCodeHistories_ArchivedYearCode",
                table: "StudentCodeHistories",
                column: "ArchivedYearCode");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCodeHistories_NewCode",
                table: "StudentCodeHistories",
                column: "NewCode");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCodeHistories_OriginalCode",
                table: "StudentCodeHistories",
                column: "OriginalCode");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCodeHistories_SourceAcademicYearId",
                table: "StudentCodeHistories",
                column: "SourceAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCodeHistories_StudentId_SourceAcademicYearId",
                table: "StudentCodeHistories",
                columns: new[] { "StudentId", "SourceAcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_ChristianName",
                table: "Students",
                column: "ChristianName");

            migrationBuilder.CreateIndex(
                name: "IX_Students_IsDeleted",
                table: "Students",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Students_LastName_FirstName",
                table: "Students",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode",
                table: "Students",
                column: "StudentCode");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSacramentRecords_StudentId_SacramentType",
                table: "StudentSacramentRecords",
                columns: new[] { "StudentId", "SacramentType" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_classes_user_id_class_id_academic_year",
                table: "teacher_classes",
                columns: new[] { "user_id", "class_id", "academic_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleHistory_AcademicYearId",
                table: "UserRoleHistory",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleHistory_ChangedAt",
                table: "UserRoleHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleHistory_ChangedByUserId",
                table: "UserRoleHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleHistory_UserId_AcademicYearId",
                table: "UserRoleHistory",
                columns: new[] { "UserId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClassTeacherId",
                table: "Users",
                column: "ClassTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagedGradeLevel",
                table: "Users",
                column: "ManagedGradeLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StudentId",
                table: "Users",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchHeadAssignments_Users_AssignedByUserId",
                table: "BranchHeadAssignments",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchHeadAssignments_Users_UserId",
                table: "BranchHeadAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassLessonPlans_Users_AssignedTeacherUserId",
                table: "ClassLessonPlans",
                column: "AssignedTeacherUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassTeachers_Users_UserId",
                table: "ClassTeachers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassTeachers_Classes_ClassRoomId",
                table: "ClassTeachers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Students_StudentId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassTeachers_AcademicYears_AcademicYearId",
                table: "ClassTeachers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassTeachers_Users_UserId",
                table: "ClassTeachers");

            migrationBuilder.DropTable(
                name: "AbsenceRequests");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "attendance_score_rules");

            migrationBuilder.DropTable(
                name: "AttendanceAuditLogs");

            migrationBuilder.DropTable(
                name: "AttendanceDisputeRequests");

            migrationBuilder.DropTable(
                name: "AttendanceRuleConfigs");

            migrationBuilder.DropTable(
                name: "BranchHeadAssignments");

            migrationBuilder.DropTable(
                name: "ClassLessonDocuments");

            migrationBuilder.DropTable(
                name: "ClassLessonPlans");

            migrationBuilder.DropTable(
                name: "ClassSchedules");

            migrationBuilder.DropTable(
                name: "FeaturePermissionRules");

            migrationBuilder.DropTable(
                name: "GradeRecords");

            migrationBuilder.DropTable(
                name: "grading_formulas");

            migrationBuilder.DropTable(
                name: "LookupViewOptions");

            migrationBuilder.DropTable(
                name: "MassCampaignRecords");

            migrationBuilder.DropTable(
                name: "parent_message_templates");

            migrationBuilder.DropTable(
                name: "PromotionRuleConfigs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RoomIncidentReports");

            migrationBuilder.DropTable(
                name: "SacramentApprovalItems");

            migrationBuilder.DropTable(
                name: "StaffQuickLookupLogs");

            migrationBuilder.DropTable(
                name: "student_grade_summaries");

            migrationBuilder.DropTable(
                name: "student_grades");

            migrationBuilder.DropTable(
                name: "student_registration_requests");

            migrationBuilder.DropTable(
                name: "StudentCodeHistories");

            migrationBuilder.DropTable(
                name: "StudentSacramentRecords");

            migrationBuilder.DropTable(
                name: "teacher_classes");

            migrationBuilder.DropTable(
                name: "UserRoleHistory");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "GradeConfigurations");

            migrationBuilder.DropTable(
                name: "MassCampaigns");

            migrationBuilder.DropTable(
                name: "SacramentApprovalBatches");

            migrationBuilder.DropTable(
                name: "grade_columns");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "ClassRoomLocations");

            migrationBuilder.DropTable(
                name: "BuildingZones");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "AcademicYears");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ClassTeachers");
        }
    }
}
