-- =========================================================================================
-- SCRIPT TẠO BẢNG & CHÈN DỮ LIỆU MẪU CHO FEAT-P5-07 (KẾ HOẠCH LỚP & VERSION NGÀY SỬA)
-- Database: PostgreSQL
-- Bảng: ClassCurriculumDocuments, ClassCurriculumVersions, ClassLessonPlans
-- =========================================================================================

-- 1. Tự động tạo bảng ClassCurriculumDocuments nếu chưa tồn tại
CREATE TABLE IF NOT EXISTS "ClassCurriculumDocuments" (
    "Id" BIGSERIAL PRIMARY KEY,
    "ClassRoomId" INT NOT NULL REFERENCES "Classes"("Id") ON DELETE CASCADE,
    "AcademicYearId" INT NOT NULL REFERENCES "AcademicYears"("Id") ON DELETE CASCADE,
    "Title" VARCHAR(255) NOT NULL DEFAULT 'Kế hoạch năm học & Phân phối chương trình',
    "Description" VARCHAR(500) NULL,
    "CurrentVersion" VARCHAR(50) NOT NULL DEFAULT 'v1.0',
    "VersionNumber" INT NOT NULL DEFAULT 1,
    "ContentJson" TEXT NULL,
    "LastAttachmentFileName" VARCHAR(255) NULL,
    "LastAttachmentFilePath" VARCHAR(500) NULL,
    "LastAttachmentFileSize" BIGINT NOT NULL DEFAULT 0,
    "LastAttachmentContentType" VARCHAR(100) NULL,
    "CreatedBy" VARCHAR(100) NULL,
    "CreatedByUserId" BIGINT NULL,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedBy" VARCHAR(100) NULL,
    "UpdatedByUserId" BIGINT NULL,
    "UpdatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS "IX_ClassCurriculumDocuments_ClassRoomId_AcademicYearId" 
ON "ClassCurriculumDocuments" ("ClassRoomId", "AcademicYearId");

CREATE INDEX IF NOT EXISTS "IX_ClassCurriculumDocuments_AcademicYearId" 
ON "ClassCurriculumDocuments" ("AcademicYearId");

-- 2. Tự động tạo bảng ClassCurriculumVersions nếu chưa tồn tại
CREATE TABLE IF NOT EXISTS "ClassCurriculumVersions" (
    "Id" BIGSERIAL PRIMARY KEY,
    "ClassCurriculumDocumentId" BIGINT NOT NULL REFERENCES "ClassCurriculumDocuments"("Id") ON DELETE CASCADE,
    "VersionNumber" INT NOT NULL DEFAULT 1,
    "VersionTag" VARCHAR(50) NOT NULL DEFAULT 'v1',
    "ChangeSummary" VARCHAR(500) NULL,
    "ContentJson" TEXT NULL,
    "FileName" VARCHAR(255) NOT NULL,
    "StoredFileName" VARCHAR(255) NOT NULL,
    "FilePath" VARCHAR(500) NOT NULL,
    "FileSize" BIGINT NOT NULL DEFAULT 0,
    "ContentType" VARCHAR(100) NULL DEFAULT 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    "CreatedBy" VARCHAR(100) NOT NULL,
    "CreatedByUserId" BIGINT NULL,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ClassCurriculumVersions_ClassCurriculumDocumentId_VersionNumber" 
ON "ClassCurriculumVersions" ("ClassCurriculumDocumentId", "VersionNumber");

CREATE INDEX IF NOT EXISTS "IX_ClassCurriculumVersions_CreatedAt" 
ON "ClassCurriculumVersions" ("CreatedAt");

-- 3. Tự động tạo hoặc bổ sung cột cho bảng ClassLessonPlans
CREATE TABLE IF NOT EXISTS "ClassLessonPlans" (
    "Id" BIGSERIAL PRIMARY KEY,
    "ClassRoomId" INT NOT NULL REFERENCES "Classes"("Id") ON DELETE CASCADE,
    "AcademicYearId" INT NOT NULL REFERENCES "AcademicYears"("Id") ON DELETE CASCADE,
    "CurriculumDocumentId" BIGINT NULL REFERENCES "ClassCurriculumDocuments"("Id") ON DELETE SET NULL,
    "WeekNumber" INT NULL,
    "Semester" INT NULL DEFAULT 1,
    "LessonDate" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "LiturgicalDay" VARCHAR(100) NULL,
    "LessonCode" VARCHAR(50) NULL,
    "TopicTitle" VARCHAR(255) NOT NULL,
    "KeyPoints" TEXT NULL,
    "AssignedTeacherUserId" BIGINT NULL,
    "AssignedTeacherName" VARCHAR(100) NULL,
    "EventNotes" VARCHAR(500) NULL,
    "IsExamDay" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsDayOff" BOOLEAN NOT NULL DEFAULT FALSE
);

ALTER TABLE "ClassLessonPlans" ADD COLUMN IF NOT EXISTS "CurriculumDocumentId" BIGINT NULL;
ALTER TABLE "ClassLessonPlans" ADD COLUMN IF NOT EXISTS "WeekNumber" INT NULL;
ALTER TABLE "ClassLessonPlans" ADD COLUMN IF NOT EXISTS "Semester" INT NULL DEFAULT 1;

CREATE INDEX IF NOT EXISTS "IX_ClassLessonPlans_ClassRoomId_LessonDate" 
ON "ClassLessonPlans" ("ClassRoomId", "LessonDate");

CREATE INDEX IF NOT EXISTS "IX_ClassLessonPlans_CurriculumDocumentId" 
ON "ClassLessonPlans" ("CurriculumDocumentId");

-- 4. Chèn dữ liệu mẫu cho Kế hoạch Lớp TS 3A với 2 Version (v1 và v2)
DO $$
DECLARE
    v_year_id INT;
    v_class_id INT;
    v_doc_id BIGINT;
    v_v1_id BIGINT;
    v_v2_id BIGINT;
    v_json_v1 TEXT;
    v_json_v2 TEXT;
BEGIN
    -- 4.1. Đảm bảo Niên khóa 2026-2027 hiện hành tồn tại
    SELECT "Id" INTO v_year_id FROM "AcademicYears" WHERE "Name" = '2026-2027' LIMIT 1;
    IF v_year_id IS NULL THEN
        INSERT INTO "AcademicYears" ("Name", "StartDate", "EndDate", "IsCurrent")
        VALUES ('2026-2027', '2026-09-01 00:00:00', '2027-06-30 00:00:00', TRUE)
        RETURNING "Id" INTO v_year_id;
    END IF;

    -- 4.2. Đảm bảo lớp học mẫu 'TS 3A' (Khối Thêm Sức) tồn tại
    SELECT "Id" INTO v_class_id FROM "Classes" 
    WHERE "Name" = 'TS 3A' AND "AcademicYearId" = v_year_id AND "IsDeleted" = FALSE LIMIT 1;
    
    IF v_class_id IS NULL THEN
        INSERT INTO "Classes" ("Name", "GradeLevel", "RoomName", "Shift", "AcademicYearId", "IsGradeLocked", "IsDeleted")
        VALUES ('TS 3A', 'Thêm Sức', 'Phòng B201', 'Sáng CN', v_year_id, FALSE, FALSE)
        RETURNING "Id" INTO v_class_id;
    END IF;

    -- 4.3. Chuẩn bị chuỗi JSON snapshot cho Version 1 (Bản đầu năm)
    v_json_v1 := '[
        {"WeekNumber":1,"LessonDate":"2026-09-06T00:00:00Z","LessonDateStr":"06/09/2026","LiturgicalDay":"CN 23 TN","LessonCode":"KHAI GIẢNG","TopicTitle":"Thánh lễ Khai giảng Năm học Giáo lý mới","KeyPoints":"Gặp gỡ GLV, sinh hoạt nội quy lớp","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":2,"LessonDate":"2026-09-13T00:00:00Z","LessonDateStr":"13/09/2026","LiturgicalDay":"CN 24 TN","LessonCode":"Bài 1","TopicTitle":"Thiên Chúa Mặc Khải và Đức Tin","KeyPoints":"Kinh Thánh và Truyền Thống Thánh - GLHTCG 50-73","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":3,"LessonDate":"2026-09-20T00:00:00Z","LessonDateStr":"20/09/2026","LiturgicalDay":"CN 25 TN","LessonCode":"Bài 2","TopicTitle":"Thiên Chúa Tạo Dựng Trời Đất","KeyPoints":"Ý nghĩa tạo dựng và phẩm giá con người","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":4,"LessonDate":"2026-09-27T00:00:00Z","LessonDateStr":"27/09/2026","LiturgicalDay":"CN 26 TN","LessonCode":"LỄ HỘI","TopicTitle":"Sinh Hoạt Đêm Hội Trăng Rằm - Trung Thu","KeyPoints":"Vui chơi cùng thiếu nhi xứ đoàn","AssignedTeacherName":"BĐH Xứ Đoàn","IsExamDay":false,"IsDayOff":true,"Semester":1},
        {"WeekNumber":5,"LessonDate":"2026-10-04T00:00:00Z","LessonDateStr":"04/10/2026","LiturgicalDay":"CN 27 TN","LessonCode":"Bài 3","TopicTitle":"Sự Sa Ngã và Lời Hứa Cứu Độ","KeyPoints":"Tội nguyên tổ và niềm hy vọng Đấng Cứu Thế","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":6,"LessonDate":"2026-10-11T00:00:00Z","LessonDateStr":"11/10/2026","LiturgicalDay":"CN 28 TN","LessonCode":"Bài 4","TopicTitle":"Mầu Nhiệm Thiên Chúa Ba Ngôi","KeyPoints":"Chúa Cha, Chúa Con và Chúa Thánh Thần","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":7,"LessonDate":"2026-10-18T00:00:00Z","LessonDateStr":"18/10/2026","LiturgicalDay":"CN 29 TN","LessonCode":"Bài 5","TopicTitle":"Chúa Giêsu Nhập Thể Làm Người","KeyPoints":"Ngôi Hai Thiên Chúa xuống thế làm người","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":8,"LessonDate":"2026-10-25T00:00:00Z","LessonDateStr":"25/10/2026","LiturgicalDay":"CN 30 TN","LessonCode":"ÔN TẬP","TopicTitle":"Hệ thống hóa giáo lý từ Bài 1 đến Bài 5","KeyPoints":"Giải đáp thắc mắc và chuẩn bị kiểm tra 1 tiết","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":9,"LessonDate":"2026-11-01T00:00:00Z","LessonDateStr":"01/11/2026","LiturgicalDay":"Lễ Các Thánh","LessonCode":"KT 1T","TopicTitle":"Kiểm Tra 1 Tiết Giáo Lý HK1","KeyPoints":"Nội dung 5 bài đầu và kinh nguyện","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":true,"IsDayOff":false,"Semester":1}
    ]';

    -- Chuỗi JSON snapshot cho Version 2 (Cập nhật lịch thi & thêm bài giảng)
    v_json_v2 := '[
        {"WeekNumber":1,"LessonDate":"2026-09-06T00:00:00Z","LessonDateStr":"06/09/2026","LiturgicalDay":"CN 23 TN","LessonCode":"KHAI GIẢNG","TopicTitle":"Thánh lễ Khai giảng Năm học Giáo lý mới","KeyPoints":"Gặp gỡ GLV, sinh hoạt nội quy lớp","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":2,"LessonDate":"2026-09-13T00:00:00Z","LessonDateStr":"13/09/2026","LiturgicalDay":"CN 24 TN","LessonCode":"Bài 1","TopicTitle":"Thiên Chúa Mặc Khải và Đức Tin","KeyPoints":"Kinh Thánh và Truyền Thống Thánh - GLHTCG 50-73","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":3,"LessonDate":"2026-09-20T00:00:00Z","LessonDateStr":"20/09/2026","LiturgicalDay":"CN 25 TN","LessonCode":"Bài 2","TopicTitle":"Thiên Chúa Tạo Dựng Trời Đất","KeyPoints":"Ý nghĩa tạo dựng và phẩm giá con người","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":4,"LessonDate":"2026-09-27T00:00:00Z","LessonDateStr":"27/09/2026","LiturgicalDay":"CN 26 TN","LessonCode":"LỄ HỘI","TopicTitle":"Sinh Hoạt Đêm Hội Trăng Rằm - Trung Thu","KeyPoints":"Vui chơi cùng thiếu nhi xứ đoàn","AssignedTeacherName":"BĐH Xứ Đoàn","IsExamDay":false,"IsDayOff":true,"Semester":1},
        {"WeekNumber":5,"LessonDate":"2026-10-04T00:00:00Z","LessonDateStr":"04/10/2026","LiturgicalDay":"CN 27 TN","LessonCode":"Bài 3","TopicTitle":"Sự Sa Ngã và Lời Hứa Cứu Độ","KeyPoints":"Tội nguyên tổ và niềm hy vọng Đấng Cứu Thế","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":6,"LessonDate":"2026-10-11T00:00:00Z","LessonDateStr":"11/10/2026","LiturgicalDay":"CN 28 TN","LessonCode":"Bài 4","TopicTitle":"Mầu Nhiệm Thiên Chúa Ba Ngôi","KeyPoints":"Chúa Cha, Chúa Con và Chúa Thánh Thần","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":7,"LessonDate":"2026-10-18T00:00:00Z","LessonDateStr":"18/10/2026","LiturgicalDay":"CN 29 TN","LessonCode":"Bài 5","TopicTitle":"Chúa Giêsu Nhập Thể Làm Người","KeyPoints":"Ngôi Hai Thiên Chúa xuống thế làm người","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":8,"LessonDate":"2026-10-25T00:00:00Z","LessonDateStr":"25/10/2026","LiturgicalDay":"CN 30 TN","LessonCode":"ÔN TẬP","TopicTitle":"Hệ thống hóa giáo lý từ Bài 1 đến Bài 5","KeyPoints":"Giải đáp thắc mắc và chuẩn bị kiểm tra 1 tiết","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":9,"LessonDate":"2026-11-01T00:00:00Z","LessonDateStr":"01/11/2026","LiturgicalDay":"Lễ Các Thánh","LessonCode":"KT 1T","TopicTitle":"Kiểm Tra 1 Tiết Giáo Lý HK1","KeyPoints":"Nội dung 5 bài đầu và kinh nguyện","AssignedTeacherName":"Anh Minh Toàn & Chị Thu Hà","IsExamDay":true,"IsDayOff":false,"Semester":1},
        {"WeekNumber":10,"LessonDate":"2026-11-08T00:00:00Z","LessonDateStr":"08/11/2026","LiturgicalDay":"CN 32 TN","LessonCode":"Bài 6","TopicTitle":"Cuộc Tử Nạn và Phục Sinh của Đức Kitô","KeyPoints":"Mầu nhiệm Vượt Qua đem lại ơn cứu độ","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":11,"LessonDate":"2026-11-15T00:00:00Z","LessonDateStr":"15/11/2026","LiturgicalDay":"CN 33 TN","LessonCode":"Bài 7","TopicTitle":"Chúa Thánh Thần - Đấng Ban Sự Sống","KeyPoints":"Ngôi Ba Thiên Chúa và 7 ơn Chúa Thánh Thần","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":12,"LessonDate":"2026-11-22T00:00:00Z","LessonDateStr":"22/11/2026","LiturgicalDay":"Lễ Chúa Kitô Vua","LessonCode":"QUAN THẦY","TopicTitle":"Mừng Lễ Bổn Mạng Xứ Đoàn Kitô Vua","KeyPoints":"Thánh lễ trọng thể và hội trại truyền thống","AssignedTeacherName":"Ban Điều Hành","IsExamDay":false,"IsDayOff":true,"Semester":1},
        {"WeekNumber":13,"LessonDate":"2026-11-29T00:00:00Z","LessonDateStr":"29/11/2026","LiturgicalDay":"CN 1 Mùa Vọng","LessonCode":"Bài 8","TopicTitle":"Hội Thánh - Dân Thiên Chúa","KeyPoints":"Bốn đặc tính của Hội Thánh: Duy nhất, Thánh thiện, Công giáo, Tông truyền","AssignedTeacherName":"Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":14,"LessonDate":"2026-12-06T00:00:00Z","LessonDateStr":"06/12/2026","LiturgicalDay":"CN 2 Mùa Vọng","LessonCode":"Bài 9","TopicTitle":"Bí Tích Thêm Sức - Ấn Tín Ơn Chúa Thánh Thần","KeyPoints":"Nghi thức, ý nghĩa và bổn phận người lãnh nhận","AssignedTeacherName":"Anh Minh Toàn","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":15,"LessonDate":"2026-12-13T00:00:00Z","LessonDateStr":"13/12/2026","LiturgicalDay":"CN 3 Mùa Vọng","LessonCode":"ÔN TẬP HK1","TopicTitle":"Ôn Tập Tổng Hợp Học Kỳ 1","KeyPoints":"Hệ thống toàn bộ giáo lý chuẩn bị thi học kỳ","AssignedTeacherName":"Anh Minh Toàn & Chị Thu Hà","IsExamDay":false,"IsDayOff":false,"Semester":1},
        {"WeekNumber":16,"LessonDate":"2026-12-20T00:00:00Z","LessonDateStr":"20/12/2026","LiturgicalDay":"CN 4 Mùa Vọng","LessonCode":"THI HK1","TopicTitle":"Thi Học Kỳ 1 Giáo Lý & Kinh Nguyện","KeyPoints":"Đề thi chung toàn khối Thêm Sức","AssignedTeacherName":"BĐH & Ban Giám Khảo","IsExamDay":true,"IsDayOff":false,"Semester":1}
    ]';

    -- 4.4. Xóa dữ liệu kế hoạch cũ của lớp 'TS 3A' nếu có để nạp mới sạch sẽ
    DELETE FROM "ClassCurriculumDocuments" WHERE "ClassRoomId" = v_class_id AND "AcademicYearId" = v_year_id;
    DELETE FROM "ClassLessonPlans" WHERE "ClassRoomId" = v_class_id AND "AcademicYearId" = v_year_id;

    -- 4.5. Chèn Tài liệu Kế hoạch Lớp hiện hành (ClassCurriculumDocuments)
    INSERT INTO "ClassCurriculumDocuments" (
        "ClassRoomId", "AcademicYearId", "Title", "Description",
        "CurrentVersion", "VersionNumber", "ContentJson",
        "LastAttachmentFileName", "LastAttachmentFilePath", "LastAttachmentFileSize",
        "LastAttachmentContentType", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "IsActive"
    )
    VALUES (
        v_class_id, v_year_id,
        'Kế hoạch bài giảng & Phân phối chương trình Lớp TS 3A',
        'Kế hoạch đào tạo giáo lý Thêm Sức năm học 2026-2027 đã được Cha Tuyên Úy phê duyệt',
        'v2', 2, v_json_v2,
        'KeHoach_TS3A_v2_20261105.xlsx', '/LessonPlans/DownloadCurriculumVersion/0', 42560,
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Anh Minh Toàn', '2026-09-06 08:30:00', 'Anh Minh Toàn', '2026-11-05 14:20:00', TRUE
    )
    RETURNING "Id" INTO v_doc_id;

    -- 4.6. Chèn Lịch sử Phiên bản 1 (v1) - Lập đầu năm học
    INSERT INTO "ClassCurriculumVersions" (
        "ClassCurriculumDocumentId", "VersionNumber", "VersionTag",
        "ChangeSummary", "ContentJson", "FileName", "StoredFileName",
        "FilePath", "FileSize", "ContentType", "CreatedBy", "CreatedAt"
    )
    VALUES (
        v_doc_id, 1, 'v1',
        'Nạp từ file kế hoạch đầu niên khóa (Bản duyệt Cha Xứ)',
        v_json_v1, 'KeHoach_TS3A_v1_20260906.xlsx', '20260906_083000_sample_v1.xlsx',
        '/LessonPlans/DownloadCurriculumVersion/0', 38420,
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Anh Minh Toàn', '2026-09-06 08:30:00'
    )
    RETURNING "Id" INTO v_v1_id;

    UPDATE "ClassCurriculumVersions" 
    SET "FilePath" = '/LessonPlans/DownloadCurriculumVersion/' || v_v1_id 
    WHERE "Id" = v_v1_id;

    -- 4.7. Chèn Lịch sử Phiên bản 2 (v2) - Cập nhật lịch thi & phân công thêm GLV
    INSERT INTO "ClassCurriculumVersions" (
        "ClassCurriculumDocumentId", "VersionNumber", "VersionTag",
        "ChangeSummary", "ContentJson", "FileName", "StoredFileName",
        "FilePath", "FileSize", "ContentType", "CreatedBy", "CreatedAt"
    )
    VALUES (
        v_doc_id, 2, 'v2',
        'Bổ sung phân công GLV phụ trách & chi tiết lịch thi HK1',
        v_json_v2, 'KeHoach_TS3A_v2_20261105.xlsx', '20261105_142000_sample_v2.xlsx',
        '/LessonPlans/DownloadCurriculumVersion/0', 42560,
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Anh Minh Toàn', '2026-11-05 14:20:00'
    )
    RETURNING "Id" INTO v_v2_id;

    UPDATE "ClassCurriculumVersions" 
    SET "FilePath" = '/LessonPlans/DownloadCurriculumVersion/' || v_v2_id 
    WHERE "Id" = v_v2_id;

    UPDATE "ClassCurriculumDocuments" 
    SET "LastAttachmentFilePath" = '/LessonPlans/DownloadCurriculumVersion/' || v_v2_id 
    WHERE "Id" = v_doc_id;

    -- 4.8. Đồng bộ sang bảng ClassLessonPlans từng tuần theo phiên bản hiện hành (v2)
    INSERT INTO "ClassLessonPlans" (
        "ClassRoomId", "AcademicYearId", "CurriculumDocumentId", "WeekNumber", "Semester",
        "LessonDate", "LiturgicalDay", "LessonCode", "TopicTitle", "KeyPoints",
        "AssignedTeacherName", "EventNotes", "IsExamDay", "IsDayOff"
    )
    VALUES
    (v_class_id, v_year_id, v_doc_id, 1, 1, '2026-09-06 00:00:00', 'CN 23 TN', 'KHAI GIẢNG', 'Thánh lễ Khai giảng Năm học Giáo lý mới', 'Gặp gỡ GLV, sinh hoạt nội quy lớp', 'Anh Minh Toàn', 'Khai giảng', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 2, 1, '2026-09-13 00:00:00', 'CN 24 TN', 'Bài 1', 'Thiên Chúa Mặc Khải và Đức Tin', 'Kinh Thánh và Truyền Thống Thánh - GLHTCG 50-73', 'Chị Thu Hà', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 3, 1, '2026-09-20 00:00:00', 'CN 25 TN', 'Bài 2', 'Thiên Chúa Tạo Dựng Trời Đất', 'Ý nghĩa tạo dựng và phẩm giá con người', 'Anh Minh Toàn', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 4, 1, '2026-09-27 00:00:00', 'CN 26 TN', 'LỄ HỘI', 'Sinh Hoạt Đêm Hội Trăng Rằm - Trung Thu', 'Vui chơi cùng thiếu nhi xứ đoàn', 'BĐH Xứ Đoàn', 'Vui Tết Trung Thu', FALSE, TRUE),
    (v_class_id, v_year_id, v_doc_id, 5, 1, '2026-10-04 00:00:00', 'CN 27 TN', 'Bài 3', 'Sự Sa Ngã và Lời Hứa Cứu Độ', 'Tội nguyên tổ và niềm hy vọng Đấng Cứu Thế', 'Chị Thu Hà', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 6, 1, '2026-10-11 00:00:00', 'CN 28 TN', 'Bài 4', 'Mầu Nhiệm Thiên Chúa Ba Ngôi', 'Chúa Cha, Chúa Con và Chúa Thánh Thần', 'Anh Minh Toàn', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 7, 1, '2026-10-18 00:00:00', 'CN 29 TN', 'Bài 5', 'Chúa Giêsu Nhập Thể Làm Người', 'Ngôi Hai Thiên Chúa xuống thế làm người', 'Chị Thu Hà', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 8, 1, '2026-10-25 00:00:00', 'CN 30 TN', 'ÔN TẬP', 'Hệ thống hóa giáo lý từ Bài 1 đến Bài 5', 'Giải đáp thắc mắc và chuẩn bị kiểm tra 1 tiết', 'Anh Minh Toàn', 'Ôn bài 1-5', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 9, 1, '2026-11-01 00:00:00', 'Lễ Các Thánh', 'KT 1T', 'Kiểm Tra 1 Tiết Giáo Lý HK1', 'Nội dung 5 bài đầu và kinh nguyện', 'Anh Minh Toàn & Chị Thu Hà', 'Kiểm tra 1 tiết', TRUE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 10, 1, '2026-11-08 00:00:00', 'CN 32 TN', 'Bài 6', 'Cuộc Tử Nạn và Phục Sinh của Đức Kitô', 'Mầu nhiệm Vượt Qua đem lại ơn cứu độ', 'Chị Thu Hà', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 11, 1, '2026-11-15 00:00:00', 'CN 33 TN', 'Bài 7', 'Chúa Thánh Thần - Đấng Ban Sự Sống', 'Ngôi Ba Thiên Chúa và 7 ơn Chúa Thánh Thần', 'Anh Minh Toàn', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 12, 1, '2026-11-22 00:00:00', 'Lễ Chúa Kitô Vua', 'QUAN THẦY', 'Mừng Lễ Bổn Mạng Xứ Đoàn Kitô Vua', 'Thánh lễ trọng thể và hội trại truyền thống', 'Ban Điều Hành', 'Lễ Bổn Mạng Xứ Đoàn', FALSE, TRUE),
    (v_class_id, v_year_id, v_doc_id, 13, 1, '2026-11-29 00:00:00', 'CN 1 Mùa Vọng', 'Bài 8', 'Hội Thánh - Dân Thiên Chúa', 'Bốn đặc tính của Hội Thánh: Duy nhất, Thánh thiện, Công giáo, Tông truyền', 'Chị Thu Hà', 'Bắt đầu Mùa Vọng', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 14, 1, '2026-12-06 00:00:00', 'CN 2 Mùa Vọng', 'Bài 9', 'Bí Tích Thêm Sức - Ấn Tín Ơn Chúa Thánh Thần', 'Nghi thức, ý nghĩa và bổn phận người lãnh nhận', 'Anh Minh Toàn', '', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 15, 1, '2026-12-13 00:00:00', 'CN 3 Mùa Vọng', 'ÔN TẬP HK1', 'Ôn Tập Tổng Hợp Học Kỳ 1', 'Hệ thống toàn bộ giáo lý chuẩn bị thi học kỳ', 'Anh Minh Toàn & Chị Thu Hà', 'Ôn thi HK1', FALSE, FALSE),
    (v_class_id, v_year_id, v_doc_id, 16, 1, '2026-12-20 00:00:00', 'CN 4 Mùa Vọng', 'THI HK1', 'Thi Học Kỳ 1 Giáo Lý & Kinh Nguyện', 'Đề thi chung toàn khối Thêm Sức', 'BĐH & Ban Giám Khảo', 'Thi Học Kỳ 1', TRUE, FALSE);

    RAISE NOTICE '>>> ĐÃ CHÈN DỮ LIỆU MẪU KẾ HOẠCH LỚP THÀNH CÔNG: ClassId=%, DocumentId=%, Version 1=%, Version 2=%', 
        v_class_id, v_doc_id, v_v1_id, v_v2_id;
END $$;

-- 5. Truy vấn kiểm tra dữ liệu sau khi chạy
SELECT "Id", "ClassRoomId", "Title", "CurrentVersion", "VersionNumber", "LastAttachmentFileName", "UpdatedAt" 
FROM "ClassCurriculumDocuments";

SELECT "Id", "ClassCurriculumDocumentId", "VersionNumber", "VersionTag", "ChangeSummary", "FileName", "CreatedAt" 
FROM "ClassCurriculumVersions" ORDER BY "VersionNumber" DESC;

SELECT "Id", "WeekNumber", "LessonDate", "LiturgicalDay", "LessonCode", "TopicTitle", "IsExamDay", "IsDayOff" 
FROM "ClassLessonPlans" ORDER BY "WeekNumber" ASC LIMIT 10;
