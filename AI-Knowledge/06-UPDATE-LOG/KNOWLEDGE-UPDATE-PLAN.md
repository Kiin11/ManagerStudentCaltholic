# KẾ HOẠCH CẬP NHẬT KHO TRI THỨC AI (KNOWLEDGE UPDATE PLAN)

- **Dự án:** `ManagerStudentCaltholic`
- **Phiên bản baseline hiện tại:** `7c61591`
- **Phiên bản mục tiêu:** `c0e828a` (Commit mới nhất trên nhánh `main`)
- **Quy tắc bắt buộc:** **KHÔNG THỰC HIỆN CẬP NHẬT Ở BƯỚC NÀY**; chỉ lập kế hoạch chi tiết và phân công lộ trình theo từng tài liệu.

---

## 1. Danh sách tài liệu cần cập nhật trong `AI-Knowledge/`

| STT | Đường dẫn tài liệu | Phân loại | Mục đích & Nội dung cần cập nhật |
|---|---|---|---|
| 1 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Modified | Bổ sung 2 tính năng Phase 5: `FEAT-P5-01` (Tra cứu nội bộ tại HomePage) và `FEAT-P5-02` (Mobile-First Web UI). |
| 2 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Modified | Bổ sung sơ đồ kiến trúc rẽ nhánh giao diện `@if (isMobile)`, chiến lược Cache 2 tầng (Redis 5 phút cho Student 360). |
| 3 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Modified | Cập nhật các thư viện/cơ chế client: Safe Area Insets, PWA reload guard, Touch Targets 44px. |
| 4 | `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Modified | Khai báo 2 bảng mới `LookupViewOptions` và `StaffQuickLookupLogs`, danh sách 5 Indexes mới. |
| 5 | `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` | Modified | Thêm cấu trúc JSON chi tiết của 2 bảng `LookupViewOptions` và `StaffQuickLookupLogs`. |
| 6 | `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Modified | Cập nhật quan hệ khóa ngoại giữa `StaffQuickLookupLogs` với `Users` và `Students`. |
| 7 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Modified | Bổ sung chỉ mục của 11 file mã nguồn mới tạo. |
| 8 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Modified | Bổ sung chỉ mục symbols cho Interface, Service, DTOs, Entities và Extension mới. |
| 9 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Modified | Bổ sung 4 endpoint mới của `HomeController`: `/Home/StaffSearchSuggestions`, `/Home/StaffStudentLookup`, `/Home/StaffRecentLookups`, `/Home/StaffLookupOptions`. |
| 10 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Modified | Bổ sung cây phụ thuộc DI của `StaffStudentLookupService` và layout mobile. |
| 11 | `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Added | Tạo mới tài liệu đặc tả luồng tra cứu tức thời 360 độ, 4 chế độ xem Option, cơ chế Redis caching và audit log. |
| 12 | `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md` | Added | Tạo mới tài liệu đặc tả kiến trúc Mobile-First, Bottom Navigation 5 phân khu, Safe Area, Offcanvas footer. |
| 13 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json` | Modified | Cập nhật commit baseline lên `c0e828a`. |
| 14 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` | Modified | Ghi nhận chi tiết kết quả của phiên phân tích và cập nhật. |
| 15 | `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md` | Modified | Xóa các mục chờ sau khi đã cập nhật xong. |

---

## 2. Kế hoạch Thực thi Từng bước (Step-by-Step Implementation Roadmap)

### **Giai đoạn 1: Cập nhật Lớp Dữ liệu & Schema (Database Layer)**
- **Bước 1.1:** Cập nhật `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` bổ sung định nghĩa schema đầy đủ cho `StaffQuickLookupLogs` và `LookupViewOptions`.
- **Bước 1.2:** Cập nhật `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` thêm sơ đồ Mermaid liên kết khóa ngoại:
  ```mermaid
  erDiagram
      Users ||--o{ StaffQuickLookupLogs : "UserId (Cascade)"
      Students ||--o{ StaffQuickLookupLogs : "StudentId (Cascade)"
  ```
- **Bước 1.3:** Cập nhật `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` bổ sung bảng kiểm kê và mô tả nghiệp vụ 2 bảng mới.

### **Giai đoạn 2: Cập nhật Chỉ mục Codebase (Code Index Layer)**
- **Bước 2.1:** Append các bản ghi file mới vào `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`.
- **Bước 2.2:** Append các class, interface, method vào `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl`.
- **Bước 2.3:** Append 4 API actions vào `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl`.
- **Bước 2.4:** Append các cạnh quan hệ (Edges) vào `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`.

### **Giai đoạn 3: Cập nhật Tài liệu Kiến trúc & Phân hệ (Architecture & Modules)**
- **Bước 3.1:** Cập nhật `AI-Knowledge/00-OVERVIEW/PROJECT.md` và `ARCHITECTURE.md` ghi nhận sự xuất hiện của Phase 5 (`FEAT-P5-01`, `FEAT-P5-02`).
- **Bước 3.2:** Khởi tạo `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md`.
- **Bước 3.3:** Khởi tạo `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md`.

### **Giai đoạn 4: Chốt Baseline & Nhật ký Cập nhật (Baseline & Audit Log)**
- **Bước 4.1:** Cập nhật `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`:
  ```json
  {
    "solution": "ManagerStudentCaltholic",
    "lastIndexedCommit": "c0e828a",
    "lastUpdatedAt": "2026-11-04T22:15:00+07:00",
    "status": "completed"
  }
  ```
- **Bước 4.2:** Điền nhật ký hoàn thành vào `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`.
- **Bước 4.3:** Làm sạch `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md`.

---

## 3. Danh sách kiểm tra Bằng chứng & Dữ liệu thiếu (Audit & Verification Checklist)

- [x] **Mã nguồn đầy đủ:** Đã có toàn bộ mã nguồn của 19 tệp thay đổi từ commit `7c61591` đến `c0e828a`.
- [x] **Không có giả định suy diễn:** Mọi dependency được liệt kê trong `IMPACT-ANALYSIS.md` đều có file, dòng code và phương thức đối chiếu cụ thể.
- [x] **Nguyên tắc bảo toàn:** Giữ nguyên tên namespace, class, method, endpoint URL thực tế, không tự tiện đổi tên.
- [x] **Trạng thái thực thi:** Bước này chỉ tạo 4 tệp báo cáo phân tích theo đúng yêu cầu; **chưa chỉnh sửa các tệp Knowledge gốc**.
