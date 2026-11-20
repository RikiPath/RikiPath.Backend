# RikiPath.Backend — Security, Code Quality & Release Standards

Bộ quy chuẩn kỹ thuật bắt buộc cho mọi đóng góp vào **RikiPath.Backend** (`ASP.NET Core 8`, `EF Core 8`, `PostgreSQL / Supabase`, `GitHub Actions`).

---

## 1. Secure Coding Standards (OWASP ASVS Alignment)

- **Authentication & Brute-Force Protection**:
  - Login không phát hành OTP đăng ký. Chỉ tài khoản chưa xác thực và còn hoạt động mới đủ điều kiện nhận mã xác thực đăng ký; email không tồn tại hoặc đã xác thực phải nhận phản hồi công khai trung tính và không phát hành/gửi mã.
  - Mã email mới được lưu dạng HMAC có nhãn mục đích (`registration-email-v1` hoặc `change-email-v1`), gắn với user ID và secret máy chủ. Mã plaintext lịch sử chỉ được nhận đến khi hết hạn gốc; không tái dùng mã của luồng này cho password reset. Đặt lại mật khẩu dùng liên kết ký riêng, không phải JWT truy cập.
  - Xác thực mã phải khóa hàng tài khoản và cập nhật trạng thái, tiêu thụ mã trong cùng transaction. Backend kiểm soát cooldown/quota; frontend countdown không phải cơ chế bảo mật. Khi chạy nhiều API instance cần rate limiter phân tán để giữ ngưỡng gửi mã thống nhất.
  - Cấu hình `AppSettings:EmailVerification` gồm `CodeLifetimeMinutes` (mặc định 30), `ResendCooldownSeconds` (60) và `MaxResendsPerHour` (5). API gửi lại trả `retryAfterSeconds` cho giao diện; phản hồi công khai không xác nhận email có tồn tại hay mã đã thực sự được chuyển phát.
  - Tuyệt đối không dùng `System.Random` (`CA5394`) cho mã OTP, token hoặc các ngữ cảnh nhạy cảm; luôn dùng `System.Security.Cryptography.RandomNumberGenerator`.
  - Mọi endpoint xác thực (`/api/auth/login`, `/api/auth/forgot-password`, `/api/auth/verify-email`, `/api/auth/resend-verification`, `/api/auth/change-password`, `/api/auth/set-password`, `/api/auth/update-email`) phải áp dụng rate limiting (`auth-sensitive`) và khóa tạm thời theo tài khoản/OTP khi nhập sai nhiều lần (`AuthService`).
  - Chính sách mật khẩu (`PasswordPolicyHelper`) kiểm tra đồng bộ khi đăng ký, đổi mật khẩu, tạo mật khẩu cho tài khoản Google và đặt lại mật khẩu (cấm mật khẩu phổ biến/chuỗi phím/trùng thông tin cá nhân, không trim ký tự của người dùng). Đổi mật khẩu tái xác minh hash hiện tại bên trong khóa hàng `FOR UPDATE` (`GetByIdForUpdateAsync`), tăng `TokenVersion`, thu hồi thiết bị FCM và ghi nhận kiểm toán (`AdminAuditLogs`).
  - JWT luôn kiểm tra chặt `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime = true`, `ValidateIssuerSigningKey = true`, `ClockSkew = TimeSpan.Zero` và đối chiếu claim `"tv"` với `UserAccount.TokenVersion` tại `OnTokenValidated`.
- **Authorization, RBAC & Ownership (IDOR Prevention)**:
  - Mọi endpoint đều mặc định yêu cầu `[Authorize]` và kiểm tra quyền sở hữu tài nguyên theo `IClaimService.GetUserClaim()` trong Application service (`MentorId == claims.Id`, `LearnerId == claims.Id`), không tin ID từ client payload/route.
  - Thao tác phá hủy hoặc quản trị (ví dụ `DELETE /api/FileStorage`, phê duyệt lịch, quản lý lớp) phải gán `[Authorize(Roles = ...)]` rõ ràng.
- **Input Validation, Uploads & Anti-Spam Concurrency**:
  - Giới hạn kích thước request body ở tầng Kestrel (`30 MB`) và controller upload (`25 MB` trên `FileStorageController`, chỉ cho phép extension an toàn, kiểm tra path traversal).
  - Tuyệt đối không dùng string concatenation tạo SQL (`CA2100 = error` trên toàn bộ production assemblies); luôn dùng EF Core LINQ / parameterized queries.
  - Các API gửi hàng loạt (như `POST /api/mentor/availability/batch`) phải thực thi trong transaction có advisory/row lock (`LockScheduleAsync`), kiểm tra idempotency key, rate limit (`mentor-availability-submit`), cooldown và quota server-side.
- **Admin CRUD, Ordering & Database Integrity**:
  - Backend là nguồn kiểm tra cuối cùng và PostgreSQL bảo vệ bất biến dữ liệu (`CHECK ("SortOrder" >= 1)`, `UNIQUE ... DEFERRABLE INITIALLY DEFERRED`, functional unique index chuẩn hóa tên/mã).
  - Reordering danh mục (`StudyCourseCatalogService`) phải chạy trong transaction có `pg_advisory_xact_lock`, kiểm tra optimistic concurrency (`Revision` / `ExpectedOrderToken` -> `409 ORDER_REVISION_CONFLICT`), không phá quan hệ lịch sử (`LEVEL_IDENTITY_LOCKED`), và trả mã lỗi nghiệp vụ `400`/`404`/`409` rõ ràng (`DISPLAY_ORDER_INVALID`, `DISPLAY_ORDER_CONFLICT`, `CERTIFICATE_ALREADY_EXISTS`, `LEVEL_ALREADY_EXISTS`, `CERTIFICATE_NOT_FOUND`), tuyệt đối không trả HTTP `500` cho lỗi validation.
- **HTTP Security Headers & Error Redaction**:
  - Middleware `RequestObservabilityAndSecurityMiddleware` gắn `X-Correlation-Id`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`.
  - Kestrel tắt `AddServerHeader`.
  - Swagger UI bị chặn ở môi trường `Production`.
  - `GlobalApiExceptionHandler` không bao giờ trả stack trace ra HTTP response; mọi chi tiết lỗi chỉ ghi vào structured logs kèm `CorrelationId`.
- **Secrets Hygiene**:
  - Không commit secret vào Git (`appsettings.json`, `appsettings.*.json`, `*.pgpass` nằm trong `.gitignore` và được quét bởi Gitleaks `.gitleaks.toml`).

---

## 2. Database, Concurrency & EF Core Migration Governance

- Tài liệu chi tiết xem tại [`README.md`](../README.md) và [`MigrationReleaseReadiness.md`](MigrationReleaseReadiness.md).
- Bắt buộc qua `dotnet build RikiPath.Backend.slnx` và `./tools/verify-migration-readiness.ps1 -SkipDatabaseCheck` trên mỗi PR (`HasPendingModelChanges() == false`, zero duplicate migration IDs, paired `.Designer.cs`).
- Startup ứng dụng (`Program.cs`) **tuyệt đối không** tự gọi `Database.Migrate()`.
- Workflow deploy (`deploy.yml`) bắt buộc chạy `verify-migration-readiness.ps1 -NoBuild -RequireSharedRemote` trước khi `Publish Web API` hoặc `Transfer files to VPS`.

---

## 3. Health Checks & Observability

- **Liveness**: `GET /health/live` (`[AllowAnonymous]`, trả `200 OK` khi tiến trình API hoạt động).
- **Readiness**: `GET /health/ready` (`[AllowAnonymous]`, kiểm tra kết nối DB thực tế và bảng cốt lõi/schema readiness qua `DatabaseSchemaReadinessService`, trả `503 Service Unavailable` nếu chưa sẵn sàng).
- **Slow Query Telemetry**: `SlowDbCommandInterceptor` tự động log cảnh báo (`> 500ms`) kèm `CorrelationId`, không ghi giá trị tham số nhạy cảm.

---

## 4. Automated Quality Gates & GitHub Admin Branch Protection

Các file cấu hình chất lượng tại gốc `RikiPath.Backend`:
- [`global.json`](../global.json): Ghim .NET SDK (`8.0.100`, `rollForward: latestMajor`).
- [`Directory.Build.props`](../Directory.Build.props): Bật `EnableNETAnalyzers`, `AnalysisLevel = 8.0-recommended`, `NuGetAudit = true` (`NuGetAuditLevel = high`).
- [`.editorconfig`](../.editorconfig): Đặt analyzer bảo mật trọng yếu thành build error (`CA2100`, `CA2300`, `CA2301`, `CA2326`, `CA5350`, `CA5351`, `CA5364`, `CA5394`).
- Workflows CI/CD:
  - [`.github/workflows/ci-quality-security.yml`](../.github/workflows/ci-quality-security.yml)
  - [`.github/workflows/migration-governance.yml`](../.github/workflows/migration-governance.yml)
  - [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml)
  - [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml)
  - [`.github/dependabot.yml`](../.github/dependabot.yml)

### Hướng dẫn cấu hình GitHub Branch Protection cho `main` (Admin Setup)

Vào **Repository Settings → Branches → Add branch protection rule** (hoặc **Rulesets**) cho nhánh `main` và bật:
1. **Require a pull request before merging** (`Required approvals >= 1`, bật `Dismiss stale pull request approvals when new commits are pushed`).
2. **Require status checks to pass before merging** (bật `Require branches to be up to date before merging`) và chọn các check bắt buộc:
   - `Build, Analyzers & NuGet Vulnerability Audit`
   - `Secret Scan (Gitleaks)`
   - `Analyze C# (security-extended)` (CodeQL)
   - `Check migration source & schema discipline` (Migration governance)
3. **Restrict who can push to matching branches** / **Block direct pushes** (kể cả `Do not allow bypassing the above settings` / `Include administrators`).
4. Trong **Settings → Environments → `production`**: bật **Required reviewers** trước khi workflow `deploy.yml` triển khai lên VPS.
5. Khai báo đầy đủ GitHub Secrets cho deploy: `APPSETTINGS_PRODUCTION`, `MIGRATION_GATE_HOST`, `MIGRATION_GATE_DATABASE`, `MIGRATION_GATE_USERNAME`, `VPS_HOST`, `VPS_USERNAME`, `VPS_PASSWORD`, và tùy chọn `API_HEALTH_BASE_URL` (ví dụ `https://api.rikipath.example`) để kiểm tra `/health/ready` sau deploy.

---

## 5. Chính sách Verification & Không Test Project (Permanent Policy)

- **Tuyệt đối không tạo hoặc duy trì test projects / test files trong `RikiPath.Backend`** (`RikiPath.Tests` đã được gỡ bỏ hoàn toàn khỏi solution và CI).
- Mọi task Backend mới chỉ tập trung vào source code (`Domain`, `Application`, `Infrastructure`, `WebApi`), kiểm tra biên dịch sạch (`dotnet build RikiPath.Backend.slnx`), security analyzers (`dotnet format analyzers --severity error`), migration safety preflight (`./tools/verify-migration-readiness.ps1`), và xác minh HTTP API trực tiếp khi cần. Không tạo Unit Test, Integration Test, Mock, Fixture, và không chạy `dotnet test`.

---

## 6. Cấu hình Kinh doanh, Bảng giá & Snapshot Giao dịch (Admin Business Config)

- **Phân loại gói học rõ ràng (`PlanCategory`)**: `SubscriptionAccessService` phân biệt gói mở bán (`self_study`, `speaking`, `writing`, `ai_grading`, `learning_path`) và gói cũ (`mentor_legacy`) qua cột `PlanCategory` thay vì dải `Id` cứng; mọi gói Admin tạo mới (`Id >= 8106`) tự động đồng bộ sang Learner `/plans`, `/purchase` và `UseAiAsync`.
- **Bảo vệ lịch sử giao dịch (Snapshot Immutability)**: `UserSubscriptions` lưu `AmountPaid`, `DurationDaysSnapshot`, `PlanNameSnapshot`, `PlanCategorySnapshot`, `PlanRevisionSnapshot` tại thời điểm checkout; chỉnh sửa/ngừng bán gói trên Admin (`Revision` concurrency token -> `409 ORDER_REVISION_CONFLICT`) không làm biến đổi đơn đã thanh toán hoặc đang chờ thanh toán.
- **PayOS & Audit Trail**: Endpoint `GET /api/admin/payments/gateway-status` chỉ trả trạng thái cấu hình boolean và URL redirect công khai, **tuyệt đối không** trả secret (`ClientId`, `ApiKey`, `ChecksumKey`); mọi thao tác cấu hình gói học, danh mục và chính sách đều ghi nhận bất biến vào `AdminAuditLogs`.
