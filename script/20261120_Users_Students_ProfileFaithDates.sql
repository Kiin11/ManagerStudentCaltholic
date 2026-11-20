-- =========================================================================================
-- PROFILE FAITH & SACRAMENT DATES MIGRATION SCRIPT - PGADMIN POSTGRESQL COMPATIBLE
-- Ngày tạo: 2026-11-20
-- Nội dung:
--   1. Bảng "Users": Bổ sung 6 cột Ngày kính & Bí tích / Tuyên hứa:
--      "FeastDay", "BaptismDate", "FirstCommunionDate", "ConfirmationDate",
--      "BaoDongPromiseDate", "CatechistPromiseDate"
--   2. Bảng "Students": Bổ sung cột "FeastDay" (Ngày kính Thánh Bổn mạng)
--   3. Đồng bộ dữ liệu sẵn có từ bảng "Students" sang "Users" qua "LinkedStudentId" / "StudentId"
-- Lưu ý: Script viết theo chuẩn idempotent, chạy trực tiếp được trên pgAdmin Query Tool.
-- =========================================================================================

BEGIN;

-- -----------------------------------------------------------------------------------------
-- 1. BẢNG "Users": BỔ SUNG CÁC CỘT NGÀY KÍNH & BÍ TÍCH / TUYÊN HỨA
-- -----------------------------------------------------------------------------------------
ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "FeastDay" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "BaptismDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "FirstCommunionDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "ConfirmationDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "BaoDongPromiseDate" TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE "Users"
    ADD COLUMN IF NOT EXISTS "CatechistPromiseDate" TIMESTAMP WITHOUT TIME ZONE NULL;

-- -----------------------------------------------------------------------------------------
-- 2. BẢNG "Students": BỔ SUNG CỘT NGÀY KÍNH
-- -----------------------------------------------------------------------------------------
ALTER TABLE "Students"
    ADD COLUMN IF NOT EXISTS "FeastDay" TIMESTAMP WITHOUT TIME ZONE NULL;

-- -----------------------------------------------------------------------------------------
-- 3. ĐỒNG BỘ DỮ LIỆU TỪ HỒ SƠ "Students" SANG "Users" (NẾU ĐÃ LIÊN KẾT)
-- -----------------------------------------------------------------------------------------
UPDATE "Users" u
SET "FeastDay" = COALESCE(u."FeastDay", s."FeastDay"),
    "BaptismDate" = COALESCE(u."BaptismDate", s."BaptismDate"),
    "FirstCommunionDate" = COALESCE(u."FirstCommunionDate", s."FirstCommunionDate"),
    "ConfirmationDate" = COALESCE(u."ConfirmationDate", s."ConfirmationDate"),
    "BaoDongPromiseDate" = COALESCE(u."BaoDongPromiseDate", s."BaoDongPromiseDate"),
    "CatechistPromiseDate" = COALESCE(u."CatechistPromiseDate", s."CatechistPromiseDate")
FROM "Students" s
WHERE s."Id" = COALESCE(u."LinkedStudentId", u."StudentId")
  AND s."IsDeleted" = FALSE;

COMMIT;
