-- =========================================================================================
-- PHASE 6 MIGRATION SCRIPT (TASK-01 & TASK-02.2) - PGADMIN POSTGRESQL COMPATIBLE
-- Ngày tạo: 2026-11-20
-- Nội dung:
--   1. Bảng "Users": Bổ sung "LinkedStudentId" (FK 1-1 tới "Students"."Id", Unique Filtered Index)
--   2. Bảng "Classes": Bổ sung cột "Session" (Morning / Afternoon) và backfill theo "GradeLevel"
--   3. Bảng "Students": Chuẩn hóa 5 cột mốc Đức tin & Tuyên hứa ("FirstCommunionDate", "BaoDongPromiseDate", "CatechistPromiseDate")
--   4. Bảng "FeaturePermissionRules": Sao chép quyền nút chức năng mặc định từ "Teacher" sang "ProbationaryTeacher"
-- Lưu ý: Script viết theo chuẩn idempotent, chạy trực tiếp được trên pgAdmin Query Tool.
-- =========================================================================================

BEGIN;

-- -----------------------------------------------------------------------------------------
-- 1. BẢNG "Users" (TASK-01.1 & TASK-01.4)
-- -----------------------------------------------------------------------------------------
ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "LinkedStudentId" BIGINT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Users_Students_LinkedStudentId'
    ) THEN
        ALTER TABLE "Users"
            ADD CONSTRAINT "FK_Users_Students_LinkedStudentId"
            FOREIGN KEY ("LinkedStudentId")
            REFERENCES "Students"("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_LinkedStudentId"
    ON "Users" ("LinkedStudentId")
    WHERE "LinkedStudentId" IS NOT NULL;

-- -----------------------------------------------------------------------------------------
-- 2. BẢNG "Classes" (TASK-01.2 & TASK-01.4)
-- -----------------------------------------------------------------------------------------
ALTER TABLE "Classes"
    ADD COLUMN IF NOT EXISTS "Session" VARCHAR(20) NOT NULL DEFAULT 'Morning';

UPDATE "Classes"
SET "Session" = CASE
    WHEN LOWER(TRIM("GradeLevel")) IN ('bao đồng', 'bao dong', 'dự bị', 'du bi', 'du_bi')
        THEN 'Afternoon'
    ELSE 'Morning'
END
WHERE "Session" IS DISTINCT FROM CASE
    WHEN LOWER(TRIM("GradeLevel")) IN ('bao đồng', 'bao dong', 'dự bị', 'du bi', 'du_bi')
        THEN 'Afternoon'
    ELSE 'Morning'
END;

CREATE INDEX IF NOT EXISTS "IX_Classes_Session"
    ON "Classes" ("Session");

CREATE INDEX IF NOT EXISTS "IX_Classes_GradeLevel_Session"
    ON "Classes" ("GradeLevel", "Session");

-- -----------------------------------------------------------------------------------------
-- 3. BẢNG "Students" - CHUẨN HÓA 5 CỘT MỐC ĐỨC TIN & TUYÊN HỨA (TASK-01.3 & TASK-01.4)
-- -----------------------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Students'
          AND column_name = 'BaoDongDate'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Students'
          AND column_name = 'BaoDongPromiseDate'
    ) THEN
        ALTER TABLE "Students"
            RENAME COLUMN "BaoDongDate" TO "BaoDongPromiseDate";
    END IF;
END $$;

ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "FirstCommunionDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "BaoDongPromiseDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "CatechistPromiseDate" TIMESTAMP WITHOUT TIME ZONE NULL;

-- Đồng bộ tương thích ngược từ bảng "StudentSacramentRecords" (nếu chưa có trên "Students")
UPDATE "Students" s
SET "FirstCommunionDate" = sub."ReceivedDate"
FROM (
    SELECT DISTINCT ON ("StudentId") "StudentId", "ReceivedDate"
    FROM "StudentSacramentRecords"
    WHERE "IsDeleted" = FALSE
      AND "SacramentType" = 2
    ORDER BY "StudentId", "ReceivedDate" DESC, "Id" DESC
) sub
WHERE s."Id" = sub."StudentId"
  AND s."FirstCommunionDate" IS NULL;

UPDATE "Students" s
SET "BaoDongPromiseDate" = sub."ReceivedDate"
FROM (
    SELECT DISTINCT ON ("StudentId") "StudentId", "ReceivedDate"
    FROM "StudentSacramentRecords"
    WHERE "IsDeleted" = FALSE
      AND "SacramentType" = 4
    ORDER BY "StudentId", "ReceivedDate" DESC, "Id" DESC
) sub
WHERE s."Id" = sub."StudentId"
  AND s."BaoDongPromiseDate" IS NULL;

CREATE INDEX IF NOT EXISTS "IX_Students_BaoDongPromiseDate"
    ON "Students" ("BaoDongPromiseDate");

CREATE INDEX IF NOT EXISTS "IX_Students_CatechistPromiseDate"
    ON "Students" ("CatechistPromiseDate");

-- -----------------------------------------------------------------------------------------
-- 4. BẢNG "FeaturePermissionRules" - ĐỒNG BỘ QUYỀN MẶC ĐỊNH CHO DỰ BỊ (TASK-02.2)
-- -----------------------------------------------------------------------------------------
UPDATE "FeaturePermissionRules"
SET "AllowedRolesCsv" = "AllowedRolesCsv" || ',ProbationaryTeacher',
    "UpdatedAt" = NOW()
WHERE ('Teacher' = ANY(string_to_array(replace("AllowedRolesCsv", ' ', ''), ',')))
  AND NOT ('ProbationaryTeacher' = ANY(string_to_array(replace("AllowedRolesCsv", ' ', ''), ',')));

COMMIT;
