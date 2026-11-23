# EXTERNAL APIs MAP

## 1. Google Drive API v3 (`FEAT-P5-08`)

- **Trạng thái:** ĐANG SỬ DỤNG (`ACTIVE`, xác minh qua `Services/GoogleDriveStorageService.cs`, `ManagerStudentCaltholic.csproj`).
- **SDK / Thư viện:**
  - `Google.Apis.Drive.v3` (`1.69.0.3769`)
  - `Google.Apis.Auth` (`1.78.0`)
- **Cơ chế Xác thực (Zero Secrets in Repo):**
  - Sử dụng Google **Service Account** thông qua `CredentialFactory.FromFile<ServiceAccountCredential>(resolvedKeyPath).ToGoogleCredential().CreateScoped(DriveService.Scope.Drive)`.
  - Đường dẫn tệp JSON key và trạng thái kích hoạt cấu hình qua `GoogleDrive:ServiceAccountKeyPath` và `GoogleDrive:Enabled` (mặc định trong repo app là `""` và `false`; khi triển khai qua `parish-infra-config`, được nạp qua `GoogleDrive__Enabled=true`, `GoogleDrive__ServiceAccountKeyPath=/app/secrets/google-service-account.json` gắn Docker volume `:ro` và `GoogleDrive__RootFolderId` / `GoogleDrive__SharedDriveId` từ `.env`). Tuyệt đối không lưu credential JSON, private key hoặc token trong mã nguồn hay tài liệu.
  - Singleton `DriveService` được làm mới tự động khi `LastWriteTimeUtc` của tệp key thay đổi.
- **Các API Operations đang gọi (`GoogleDriveStorageService`):**
  1. `Files.List` (Tìm thư mục theo tên & parent ID): Dùng `SupportsAllDrives = true`, `IncludeItemsFromAllDrives = true`; khi cấu hình `GoogleDrive:SharedDriveId`, bổ sung `Corpora = "drive"` và `DriveId`. Kết quả thư mục được cache 30 phút trong `IMemoryCache` kèm khóa `SemaphoreSlim` chống tạo trùng.
  2. `Files.Create` (Tạo thư mục & Resumable Upload tệp): Đặt `SupportsAllDrives = true`, `Fields = "id, name, size, parents"`; truyền luồng qua `UploadAsync(ct)` với `ChunkSize` căn chỉnh bội số `256 KB` (`ResumableUpload.MinimumChunkSize`), tối đa `2 MB` (`MaxDriveChunkBytes`). Tự động retry tối đa `MaxRetryAttempts` (mặc định `3`) với exponential backoff `500ms * 2^(attempt-1)`.
  3. `Files.Get` (Tải tệp streaming cho Auth Proxy `/CloudMedia/File/{id}`): Đặt `SupportsAllDrives = true`, tải qua `DownloadAsync(tmpStream, ct)` vào tệp tạm `FileOptions.DeleteOnClose` (nếu `<= 2 MB` chuyển về `MemoryStream`; nếu `> 2 MB` stream trực tiếp từ `FileStream`).
  4. `Files.Delete` (Xóa tệp vật lý khi xóa dữ liệu): Đặt `SupportsAllDrives = true`, bỏ qua `HttpStatusCode.NotFound` (404) và vẫn tiếp tục xóa mềm bản ghi trong `CloudMediaFiles`.
- **Phân cấp Thư mục & Hỗ trợ Shared Drive:**
  - Hỗ trợ `ExplicitParentFolderId` (ví dụ `DriveLogUpload:LogsFolderId`), `GoogleDrive:RootFolderId`, `GoogleDrive:SharedDriveId` và `GoogleDrive:RootFolderName` (`QLGL_Storage_Root`).
  - Các phân nhánh thư mục nghiệp vụ: `{AcademicYear}/LessonPlans`, `{AcademicYear}/Incidents`, `Students_Avatars`, `Sacraments_Docs`, `{AcademicYear}/Announcements`, `{AcademicYear}/AbsenceRequests`, `System_Logs/{yyyy-MM}`, `Legacy_Orphans/lesson_plans`.
- **Xử lý Lỗi & Dự phòng (`LOCAL_FALLBACK`):**
  - Khi tắt Drive, thiếu Service Account key, thiếu Root/SharedDrive hoặc upload lỗi sau số lần retry: với tệp nghiệp vụ (`AllowLocalFallback = true`), lưu vào `PrivateUploads/cloud_fallback/{Category}/{StoredFileName}` (`StorageProvider = LOCAL_FALLBACK`, `SyncStatus = RETRY`) để đồng bộ bù sau qua `/Admin/Logs/RetryFallbackSync` hoặc `LocalMediaMigrationService`. Với log backjob và migration cũ, dùng `AllowLocalFallback = false`.

---

## 2. Các API Bên Thứ Ba Khác (SMS / Zalo ZNS / Payment)

Dựa vào việc rà soát toàn bộ tệp tin trong thư mục `Controllers` và `Services`, **không phát hiện tích hợp gọi ra nào tới SMS Gateway, Zalo ZNS hoặc cổng thanh toán bên ngoài**:
- Thay vì gửi Zalo qua API (ZNS), hệ thống tạo ra các mẫu tin nhắn (`ParentMessageTemplates`) có điền sẵn biến số, cho phép người dùng bấm nút "1-Click Copy" và dán thủ công vào ứng dụng chat.
- Các endpoint nội bộ dùng cho Frontend/PWA (AJAX/Fetch) sử dụng cơ chế bảo mật Cookie/JWT Bearer và Anti-CSRF `X-CSRF-TOKEN`.
