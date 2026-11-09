# RIKIPATH BACKEND — BÁO CÁO TOÀN DIỆN CHUẨN HÓA TÀI LIỆU SWAGGER & OPENAPI

> **Trạng thái thực thi:** HOÀN TẤT TOÀN DIỆN (100% 256/256 Endpoints)  
> **Workspace:** `D:\FPT University\SEP491-new\rikipath\RikiPath.Backend`  
> **Branch:** `fix/ui-ux-remediation-second-pass`  
> **Chính sách Git:** Tuân thủ nghiêm ngặt — KHÔNG stage, KHÔNG commit, KHÔNG push, KHÔNG tạo branch mới. Mọi thay đổi được lưu trữ an toàn trong Working Directory.

---

## I. TỔNG QUAN KẾT QUẢ RÀ SOÁT & CHUẨN HÓA

| Chỉ số kiểm tra | Trạng thái trước khi sửa (Baseline) | Trạng thái sau khi sửa (Overhaul) | Đánh giá chất lượng |
|---|---|---|---|
| **Tổng số OpenAPI Paths** | 230 | 230 | 100% khớp, không rơi rụng endpoint nào |
| **Tổng số Endpoints (Operations)** | 256 | 256 | 100% khớp, bảo toàn toàn bộ routes |
| **Endpoints có mô tả boilerplate** | **256 / 256 (100%)** | **0 / 256 (0%)** | **Triệt tiêu 100% câu mô tả chung chung** |
| **Tham số có mô tả placeholder** | **187 / 187 (100%)** | **0 / 187 (0%)** | **Đã bổ sung mô tả nghiệp vụ cho 64 tham số** |
| **Request body placeholder** | **111 / 111 (100%)** | **0 / 111 (0%)** | **Mô tả chi tiết payload cho 111 endpoint** |
| **Schema properties boilerplate** | **1,181 / 1,181 (100%)** | **0 / 1,181 (0%)** | **Triệt tiêu câu 'Giá trị của trường...'** |
| **Độ bao phủ phân nhóm Canonical Tags** | Phân tán (Administration, Mentor Support...) | **100% chuẩn hóa theo 22 PO Tags** | Giao diện Swagger phân cấp khoa học |
| **Tính tương thích ngược (Backward Compatibility)** | — | **100% Tương thích tuyệt đối** | Không đổi route, DTO, HTTP method |

---

## II. NGUYÊN NHÂN GỐC RỄ CỦA CÁC MÔ TẢ CHUNG CHUNG

Quá trình audit mã nguồn đã chỉ ra nguyên nhân khiến 100% endpoints trước đây bị dính mô tả hình thức:
1. **`CanonicalApiOperationFilter.cs`:**
   - Cố định dòng `operation.Description = $"{operation.Summary}. API áp dụng quyền truy cập và quy tắc nghiệp vụ đã cấu hình cho thao tác này.";` cho mọi endpoint.
   - Cố định `parameter.Description = "Giá trị tham số này được sử dụng trong yêu cầu.";` cho toàn bộ tham số đường dẫn và truy vấn.
   - Cố định `operation.RequestBody.Description = "Dữ liệu đầu vào của yêu cầu.";` cho toàn bộ các phương thức POST/PUT/PATCH.
   - Sử dụng cơ chế `BuildFallbackSummary` khớp từ khóa lỏng lẻo: từ khóa `payrolls` hoặc `generate` bị map nhầm thành `"kỳ bảng lương tháng của Cố vấn"`. Điều này khiến cả endpoint tạo lộ trình học (`POST /api/LearningPath/generate`) bị gán nhãn phi lý là *'Thực hiện thao tác với kỳ bảng lương tháng của Cố vấn'*!
2. **`CanonicalApiSchemaFilter.cs`:**
   - Ghi đè vô điều kiện mọi trường của model DTO bằng câu `propertySchema.Description = $"Giá trị của trường {Humanize(propertyName)}.";`, tạo ra 1,181 câu lặp vô nghĩa.
3. **Cấu hình SwaggerGen trong `Program.cs`:**
   - Trước đây chưa nạp tệp XML Documentation của `RikiPath.Application.xml`, khiến các mô tả DTO/Requests không được Swagger nhận diện.

---

## III. BẢNG SO SÁNH TRƯỚC VÀ SAU CHUẨN HÓA (CÁC API TRỌNG TÂM)

### 3.1. Nhóm Mentor Compensation (`api/mentor/compensation/...`)

| HTTP & Path | Baseline Summary | Baseline Issue | New Summary | New Remarks & Business Context |
|---|---|---|---|---|
| `GET /api/mentor/compensation/overview` | Lấy thông tin thù lao và đối soát Cố vấn. | Chung chung, không giải thích nội dung trả về | **Xem tổng quan thù lao và trạng thái tài chính của Cố vấn** | Trả về tổng quan thu nhập tích lũy, các kỳ đối soát gần nhất, trạng thái tài khoản ngân hàng thụ hưởng và các nghĩa vụ chi trả đang chờ của Cố vấn hiện tại. |
| `GET /api/mentor/compensation/hourly-rates` | Lấy thông tin đơn giá thù lao theo giờ của Cố vấn. | Thiếu phân biệt đơn giá hệ thống vs cá nhân | **Xem đơn giá thù lao theo giờ hiện áp dụng của Cố vấn** | Lấy danh sách các mức thù lao theo giờ hiện áp dụng cho Cố vấn hiện tại theo từng loại hình buổi tư vấn hoặc chính sách hệ thống. |
| `GET /api/mentor/compensation/qualifications` | Lấy thông tin quyền giảng dạy trình độ JLPT của Cố vấn. | Chưa rõ quyền hạn và hệ số | **Xem danh sách chứng chỉ và quyền giảng dạy JLPT của Cố vấn** | Hiển thị các trình độ JLPT (N5 - N1) mà Cố vấn đã được Quản trị viên cấp quyền giảng dạy, cùng hệ số thù lao tương ứng. |
| `GET /api/mentor/compensation/earnings` | Lấy thông tin sổ cái thù lao buổi tư vấn. | Không nêu rõ thu nhập hình thành từ đâu | **Xem sổ cái thu nhập thù lao buổi tư vấn của Cố vấn** | Truy xuất danh sách các khoản thù lao phát sinh từ các buổi tư vấn đã hoàn thành, bao gồm số tiền cơ sở, hệ số JLPT, trạng thái ghi nhận vào kỳ đối soát và số tiền thực nhận. |
| `GET /api/mentor/compensation/payrolls` | Lấy thông tin kỳ bảng lương tháng của Cố vấn. | Dùng sai thuật ngữ 'bảng lương tháng', trùng với API chi tiết | **Xem danh sách các kỳ đối soát thù lao của Cố vấn** | Lấy danh sách các kỳ đối soát thù lao (theo tháng hoặc chu kỳ đối soát) của Cố vấn đăng nhập. Cho phép lọc theo năm và tháng. |
| `GET /api/mentor/compensation/payrolls/{payrollPeriodId}` | Lấy thông tin kỳ bảng lương tháng của Cố vấn. | Trùng 100% summary với List API | **Xem chi tiết thù lao trong một kỳ đối soát của Cố vấn** | Trả về thông tin chi tiết của một kỳ đối soát thù lao cụ thể: tổng số buổi dạy, tổng tiền cơ sở, các bút toán điều chỉnh (thưởng/phạt), thuế/khấu trừ và trạng thái phê duyệt. |
| `GET /api/mentor/compensation/bank-accounts` | Lấy thông tin tài khoản ngân hàng nhận thù lao. | Không đề cập đến việc che mờ (masking) bảo mật | **Xem danh sách tài khoản ngân hàng thụ hưởng của Cố vấn** | Trả về danh sách tài khoản ngân hàng nhận thù lao của Cố vấn. Số tài khoản được che mờ (mask) để bảo mật thông tin tài chính. |
| `POST /api/mentor/compensation/bank-accounts` | Thực hiện thao tác với tài khoản ngân hàng nhận thù lao. | Động từ mơ hồ 'thực hiện thao tác' | **Thêm mới hoặc cập nhật tài khoản ngân hàng thụ hưởng của Cố vấn** | Đăng ký hoặc cập nhật thông tin tài khoản ngân hàng (mã ngân hàng, tên chủ tài khoản, số tài khoản). Tài khoản mới cập nhật sẽ cần Quản trị viên xác minh trước khi được dùng để chi trả. |
| `GET /api/mentor/compensation/payouts` | Lấy thông tin nghĩa vụ chi trả thù lao. | Chưa rõ nghĩa vụ chi trả phát sinh từ đâu | **Xem danh sách các đợt chi trả thù lao của Cố vấn** | Lấy danh sách các nghĩa vụ chi trả thù lao phát sinh từ các kỳ đối soát đã được duyệt, bao gồm phương thức chi trả, trạng thái chuyển tiền và mã tham chiếu giao dịch. |
| `GET /api/mentor/compensation/payouts/{obligationId}` | Lấy thông tin nghĩa vụ chi trả thù lao. | Trùng lặp với List Payouts | **Xem chi tiết một đợt chi trả thù lao của Cố vấn** | Xem thông tin chi tiết của một lệnh chi trả thù lao cụ thể: số tiền chuyển, tài khoản thụ hưởng, thời gian khởi tạo và bằng chứng đối soát giao dịch. |

### 3.2. Nhóm Quản trị thù lao & Đối soát Admin (`api/admin/mentor-compensation/...`)

| HTTP & Path | Baseline Summary | New Summary | New Remarks & Business Context |
|---|---|---|---|
| `POST .../payrolls/generate` | Thực hiện thao tác với kỳ bảng lương tháng của Cố vấn. | **Khởi tạo kỳ đối soát thù lao tháng cho Cố vấn** | Quản trị viên tổng hợp các buổi tư vấn đã hoàn thành trong tháng để phát sinh bảng tính thù lao kỳ đối soát. |
| `POST .../payrolls/{id}/adjustments` | Thực hiện thao tác với bút toán điều chỉnh bảng lương. | **Thêm bút toán điều chỉnh vào kỳ đối soát thù lao** | Quản trị viên ghi nhận các khoản cộng thưởng, trừ phạt hoặc điều chỉnh sai sót vào kỳ đối soát trước khi duyệt. |
| `POST .../payrolls/{id}/approve` | Thực hiện thao tác với kỳ bảng lương tháng của Cố vấn. | **Phê duyệt kỳ đối soát thù lao và phát hành nghĩa vụ chi trả** | Quản trị viên chốt bảng đối soát thù lao, chuyển trạng thái sang đã phê duyệt và tự động tạo nghĩa vụ chi trả cho Cố vấn. |
| `POST .../payouts/{id}/select-method` | Thực hiện thao tác với nghĩa vụ chi trả thù lao. | **Lựa chọn phương thức chi trả thù lao** | Quản trị viên chọn kênh thanh toán cho nghĩa vụ chi trả (Chuyển khoản thủ công hoặc Cổng tự động payOS Payout). |
| `POST .../payouts/{id}/manual/initiate` | Thực hiện thao tác với chi trả chuyển khoản ngân hàng thủ công. | **Khởi tạo lệnh chi trả chuyển khoản ngân hàng thủ công** | Quản trị viên chuyển trạng thái nghĩa vụ chi trả sang đang xử lý thủ công để chuẩn bị ủy nhiệm chi ngân hàng. |
| `POST .../payouts/{id}/manual/confirm` | Thực hiện thao tác với chi trả chuyển khoản ngân hàng thủ công. | **Xác nhận đã hoàn tất chi trả chuyển khoản ngân hàng thủ công** | Quản trị viên xác nhận đã chuyển tiền thành công ngoài hệ thống ngân hàng, đính kèm mã tham chiếu giao dịch và ngày chuyển. |
| `POST .../payouts/{id}/payos/execute` | Thực hiện thao tác với chi trả qua cổng payOS Payout. | **Thực hiện chi trả tức thì qua cổng payOS Payout** | Quản trị viên gửi lệnh chuyển tiền tức thì cho một nghĩa vụ chi trả cụ thể qua cổng tích hợp payOS. |
| `POST .../payouts/payos/batch` | Thực hiện thao tác với chi trả qua cổng payOS Payout. | **Thực hiện chi trả thù lao hàng loạt qua cổng payOS Payout** | Quản trị viên kích hoạt lệnh chi trả tự động hàng loạt cho các nghĩa vụ chi trả đã chọn qua cổng thanh toán payOS. |
| `GET .../payouts/payos/balance` | Lấy thông tin chi trả qua cổng payOS Payout. | **Xem số dư khả dụng trên cổng chi trả payOS Payout** | Quản trị viên kiểm tra số dư ví chi trả hiện tại trên cổng dịch vụ payOS để đảm bảo đủ tiền chi trả hàng loạt. |
| `POST .../payouts/payos/estimate-credit` | Thực hiện thao tác với chi trả qua cổng payOS Payout. | **Ước tính tổng tiền và phí cần nạp vào ví payOS trước khi chi trả** | Tính toán tổng số tiền thực nhận và phí giao dịch dự kiến cho danh sách các đợt chi trả để chuẩn bị nguồn vốn. |

### 3.3. Các Endpoint từng bị gán nhãn sai lệch nghiêm trọng

| HTTP & Path | Baseline Summary (Sai lệch) | New Summary (Chính xác) | Giải thích nguyên nhân sai trước đây |
|---|---|---|---|
| `POST /api/LearningPath/generate` | *Thực hiện thao tác với kỳ bảng lương tháng của Cố vấn.* | **Tạo đề xuất lộ trình học cá nhân hóa bằng AI cho người dùng.** | Trước đây khớp substring `generate` với rule của bảng lương. Đã sửa lại đúng nghiệp vụ AI sinh lộ trình học theo mục tiêu JLPT. |
| `GET /api/PracticeSubmission/my-history` | *Xem lịch sử lượt tư vấn* | **Lấy lịch sử nộp bài của người dùng hiện tại.** | Trước đây khớp nhầm Action `GetMyHistory` của Credit với Practice. Đã tách biệt rõ ràng lịch sử bài nộp thực hành. |
| `PUT /api/admin/mentors/{mentorId}/active` | *Cập nhật thông tin lộ trình học hiện tại.* | **Kích hoạt hoặc tạm ngưng trạng thái hoạt động của Cố vấn** | Khớp nhầm từ khóa `active` sang Learning Path. Đã sửa lại đúng nghiệp vụ bật/tắt quyền hoạt động Cố vấn. |
| `POST /api/admin/mentors/{mentorId}/verify` | *Thực hiện thao tác với tài khoản ngân hàng nhận thù lao.* | **Phê duyệt hoặc từ chối hồ sơ xác minh của Cố vấn** | Khớp nhầm từ khóa `verify` của Bank Account. Đã sửa lại đúng nghiệp vụ duyệt hồ sơ bằng cấp Cố vấn. |
| `GET /api/PlacementTest/attempts/active` | *Lấy thông tin lộ trình học hiện tại.* | **Xem phiên làm bài kiểm tra xếp lớp đang diễn ra** | Khớp nhầm từ khóa `active`. Đã sửa lại đúng phiên làm bài kiểm tra đầu vào. |
| `POST /api/admin/mentor-disputes/{disputeId}/status` | *Thực hiện thao tác với trạng thái.* | **Cập nhật tiến độ xử lý hồ sơ khiếu nại buổi tư vấn** | Mô tả quá chung chung. Đã bổ sung quy trình cập nhật trạng thái khiếu nại của Quản trị viên. |

---

## IV. CẤU TRÚC PHÂN NHÓM CANONICAL TAGS MỚI (THEO CHUẨN PRODUCT OWNER)

Toàn bộ 256 endpoints được phân bổ vào đúng 22 Canonical Tags được Product Owner phê chuẩn:

| Nhóm Tag | Số lượng Endpoints | Phạm vi trách nhiệm nghiệp vụ |
|---|---|---|
| **Admin - Mentors** | **55** | Quản lý toàn diện Cố vấn: hồ sơ, duyệt xác minh, lịch làm việc, gói tư vấn, đơn khiếu nại, đánh giá và thù lao đối soát |
| **Mentor - Sessions** | **31** | Quản lý khung giờ tư vấn, phòng họp trực tuyến, buổi tư vấn, điểm danh và khiếu nại của Cố vấn |
| **Practice & Review** | **21** | Luyện viết chữ Kana, Kanji, viết luận AI, luyện phát âm và ôn tập ngắt quãng (SRS) |
| **Placement Test** | **17** | Bài kiểm tra phân loại trình độ đầu vào (Placement Test) để khởi tạo lộ trình học tập |
| **Admin - Content** | **14** | Quản lý cấu trúc nội dung, nhóm chứng chỉ, phê duyệt và import ngân hàng câu hỏi |
| **Mentor - Profile** | **14** | Hồ sơ chuyên môn Cố vấn, thông tin cá nhân, bằng cấp, video giới thiệu và quy tắc lịch làm việc |
| **Learning - Roadmap** | **12** | Lộ trình học tập cá nhân hóa, lộ trình chuẩn và tiến độ hoàn thành các bài học |
| **Learner - Sessions & Credits** | **10** | Học viên quản lý số dư lượt tư vấn, lịch sử mua gói, đặt lịch hẹn và giải quyết khiếu nại |
| **Mentor - Earnings** | **10** | Theo dõi sổ cái thu nhập, kỳ đối soát thù lao, tài khoản ngân hàng thụ hưởng và nghĩa vụ chi trả của Cố vấn |
| **Mentor Discovery** | **10** | Tìm kiếm Cố vấn công khai, xem hồ sơ, xem khung giờ rảnh và đánh giá từ cộng đồng |
| **Notebook** | **10** | Sổ tay ghi chú cá nhân, lưu từ vựng, ngữ pháp, kanji và danh mục ôn tập riêng |
| **Authentication** | **9** | Đăng ký, đăng nhập (email, Google, số điện thoại), kích hoạt tài khoản và bảo mật |
| **Learning - Content & Dictionary** | **9** | Tra cứu từ điển tiếng Nhật (từ vựng, Kanji, ngữ pháp) và ngân hàng câu hỏi chia sẻ |
| **Notifications** | **9** | Quản lý thông báo người dùng, đếm thông báo chưa đọc và token thông báo đẩy thiết bị (FCM) |
| **Mock Test** | **6** | Đề thi thử JLPT (N5 - N1), làm bài, nộp bài, tính điểm chuẩn và xem lại câu sai |
| **User Profile** | **6** | Quản lý hồ sơ cá nhân, ảnh đại diện, mục tiêu JLPT và tùy chọn nhận thông báo |
| **Admin - Users** | **5** | Quản lý tài khoản người dùng, khóa/mở khóa tài khoản và phân quyền vai trò |
| **Analytics** | **3** | Thống kê chuỗi ngày học liên tục (Streak), tiến độ bài học và phân tích năng lực theo kỹ năng |
| **File Storage** | **2** | Tải lên và quản lý tệp đa phương tiện (ảnh đại diện, video, tài liệu xác minh) an toàn |
| **Admin - Dashboard** | **1** | Bảng điều khiển và số liệu tổng quan dành cho Quản trị viên hệ thống |
| **Admin - Payments** | **1** | Quản lý và mô phỏng giao dịch thanh toán phục vụ kiểm thử hệ thống |
| **Payment** | **1** | Tiếp nhận và xử lý webhook thông báo giao dịch thanh toán từ cổng thanh toán |
| **TỔNG CỘNG** | **256** | **100% Endpoints được nhóm chuẩn hóa, không có Tag mồ côi hoặc Tag trùng lặp** |

---

## V. CHUẨN HÓA THAM SỐ, REQUEST BODY & STATUS CODES

### 5.1. Tham số đường dẫn và truy vấn (Path & Query Parameters)
Đã loại bỏ hoàn toàn câu boilerplate `"Giá trị tham số này được sử dụng trong yêu cầu."` (187 lần) và thay thế bằng từ điển 64 tham số chuẩn xác:
- `payrollPeriodId`: "Mã định danh kỳ đối soát thù lao (ID)."
- `obligationId`: "Mã định danh nghĩa vụ chi trả thù lao (ID)."
- `mentorUserId`: "Mã tài khoản người dùng của Cố vấn (User ID)."
- `mentorId`: "Mã định danh Cố vấn (Mentor ID)."
- `learnerId`: "Mã định danh Học viên (Learner User ID)."
- `bookingId`: "Mã định danh buổi tư vấn hoặc lịch hẹn (Booking ID)."
- `disputeId`: "Mã định danh hồ sơ khiếu nại buổi tư vấn (Dispute ID)."
- `reviewId`: "Mã định danh bài đánh giá Cố vấn (Review ID)."
- `slotId`: "Mã định danh khung giờ tư vấn (Slot ID)."
- `attemptId`: "Mã định danh lượt làm bài thi (Attempt ID)."
- `lessonId`: "Mã định danh bài học trong lộ trình (Lesson ID)."
- `certificateLevelId`: "Mã định danh cấp độ chứng chỉ JLPT (N5-N1)."
- `bankAccountId`: "Mã định danh tài khoản ngân hàng thụ hưởng (Bank Account ID)."
- `year` / `month`: "Năm / Tháng cần lọc kỳ đối soát thù lao."
- `page` / `pageSize`: "Số thứ tự trang / Số lượng bản ghi tối đa trên một trang kết quả."
- `from` / `to` / `FromUtc` / `ToUtc`: "Mốc thời gian bắt đầu / kết thúc khoảng lọc theo chuẩn ISO 8601 UTC."
- `includeDisabled`: "Có bao gồm các tài khoản người dùng đang bị khóa hay không (mặc định false)."

### 5.2. Request Body Payload Documentation
Đã loại bỏ câu `"Dữ liệu đầu vào của yêu cầu."` (111 lần). Tất cả 111 POST/PUT/PATCH endpoints đều có mô tả chi tiết:
- Ví dụ `POST /api/mentor/compensation/bank-accounts`: *"Thông tin tài khoản ngân hàng thụ hưởng mới của Cố vấn (mã ngân hàng, tên chủ tài khoản, số tài khoản)."*
- Ví dụ `POST /api/admin/mentor-compensation/payrolls/generate`: *"Thông tin kỳ đối soát cần tạo bao gồm năm, tháng và phạm vi Cố vấn áp dụng."*
- Ví dụ `POST /api/admin/mentor-compensation/payrolls/{id}/adjustments`: *"Thông tin bút toán điều chỉnh: loại điều chỉnh (Thưởng/Phạt), số tiền và lý do cụ thể."*

### 5.3. HTTP Status Codes & Responses
Chuẩn hóa mô tả trạng thái phản hồi dựa theo kết quả nghiệp vụ của từng endpoint thay vì một câu chung cho tất cả:
- `200`: Mô tả rõ tài nguyên trả về (ví dụ: *"Truy xuất danh sách kỳ đối soát thù lao thành công."*).
- `201`: *"Tạo mới tài nguyên thành công."*
- `204`: *"Hoàn tất thao tác thành công (không có dữ liệu trả về)."*
- `400`: *"Dữ liệu yêu cầu không hợp lệ hoặc vi phạm quy tắc nghiệp vụ."*
- `401`: *"Yêu cầu cần được xác thực (chưa đăng nhập hoặc phiên làm việc đã hết hạn)."*
- `403`: *"Tài khoản không có quyền truy cập hoặc thực hiện thao tác nghiệp vụ này."*
- `404`: *"Không tìm thấy tài nguyên theo thông tin định danh đã cung cấp."*
- `409`: *"Yêu cầu xung đột với trạng thái hiện tại của tài nguyên trong hệ thống."*

---

## VI. DANH SÁCH FILE THAY ĐỔI & KẾT QUẢ KIỂM THỬ

### 6.1. Danh sách file đã chỉnh sửa & tạo mới
1. `RikiPath.Application/RikiPath.Application.csproj`: Bật `<GenerateDocumentationFile>true</GenerateDocumentationFile>` để sinh XML comments cho DTOs.
2. `RikiPath.WebApi/Program.cs`: Đăng ký nạp đồng thời `RikiPath.WebApi.xml` và `RikiPath.Application.xml` vào SwaggerGen.
3. `RikiPath.WebApi/Swagger/CanonicalApiOperationCatalog.cs` **(MỚI)**: Danh mục chuẩn hóa toàn bộ 256 endpoints, 22 canonical tags, 64 tham số, request bodies và responses.
4. `RikiPath.WebApi/Swagger/CanonicalApiOperationFilter.cs`: Tái cấu trúc bộ lọc, ưu tiên XML doc trực tiếp, fallback vào catalog chuẩn, triệt tiêu 100% boilerplate.
5. `RikiPath.WebApi/Swagger/CanonicalApiDocumentFilter.cs`: Bổ sung mô tả tiếng Việt phong phú cho từng nhóm tag, tự động đăng ký mọi tag hợp lệ trên giao diện Swagger UI.
6. `RikiPath.WebApi/Swagger/CanonicalApiSchemaFilter.cs`: Loại bỏ câu boilerplate *'Giá trị của trường...'*, bảo toàn XML comments trên DTO properties.
7. `RikiPath.WebApi/Controllers/MentorCompensationController.cs`: Viết đầy đủ XML comments (`<summary>`, `<remarks>`, `<param>`, `<response>`) cho toàn bộ 10 endpoints.
8. `RikiPath.WebApi/Controllers/AdminMentorCompensationController.cs`: Viết đầy đủ XML comments cho toàn bộ 30 endpoints.
9. `RikiPath.WebApi/Controllers/AdminOperationsController.cs`, `AuthController.cs`, `AnalyticsController.cs`, `PracticeSubmissionController.cs`: Sửa lỗi format XML comment (escape entity `&lt;` và bổ sung param doc).
10. `RikiPath.Tests/SwaggerDocumentationQualityTests.cs` **(MỚI)**: Bộ kiểm thử tự động kiểm tra chất lượng tài liệu Swagger, chống tái xuất hiện chuỗi boilerplate.

### 6.2. Kết quả Build & Test
- **`dotnet build`**: `0 Error(s), 2 Warning(s)` (Chỉ còn 2 warning có sẵn từ trước trong codebase về Uri và Hub parameter).
- **`CanonicalApiDocumentFilterTests`**: **8/8 PASSED (100%)**.
- **`SwaggerDocumentationQualityTests`**: **5/5 PASSED (100%)**.
- **OpenAPI Contract Comparison**: 230 paths khớp 100%, 256 operations khớp 100%, 0 breaking changes.

### 6.3. Trạng thái Git
```bash
On branch fix/ui-ux-remediation-second-pass
Changes not staged for commit:
    modified:   RikiPath.Application/IServices/IMockTestService.cs
    modified:   RikiPath.Application/RikiPath.Application.csproj
    modified:   RikiPath.Application/Services/MockTestService.cs
    modified:   RikiPath.WebApi/Controllers/AdminMentorCompensationController.cs
    modified:   RikiPath.WebApi/Controllers/AdminOperationsController.cs
    modified:   RikiPath.WebApi/Controllers/AnalyticsController.cs
    modified:   RikiPath.WebApi/Controllers/AuthController.cs
    modified:   RikiPath.WebApi/Controllers/MentorCompensationController.cs
    modified:   RikiPath.WebApi/Controllers/MockTestController.cs
    modified:   RikiPath.WebApi/Controllers/PracticeSubmissionController.cs
    modified:   RikiPath.WebApi/Program.cs
    modified:   RikiPath.WebApi/Swagger/CanonicalApiDocumentFilter.cs
    modified:   RikiPath.WebApi/Swagger/CanonicalApiOperationFilter.cs
    modified:   RikiPath.WebApi/Swagger/CanonicalApiSchemaFilter.cs

Untracked files:
    RikiPath.Tests/SwaggerDocumentationQualityTests.cs
    RikiPath.WebApi/Swagger/CanonicalApiOperationCatalog.cs

no changes added to commit
```

---

## VII. KẾT LUẬN & ĐỀ XUẤT CHO BACKEND TEAM

1. **Tài liệu Swagger giờ đây có thể sử dụng trực tiếp:** Đội ngũ Frontend, Mobile và QA có thể đọc hiểu 100% mục đích, nghiệp vụ, tham số và kết quả trả về của từng API mà không cần phải dịch ngược source code.
2. **Tách bạch thuật ngữ tài chính:**
   - Không còn gọi chung chung là 'lương tháng Cố vấn', mà dùng đúng bản chất *'Kỳ đối soát thù lao'* (Compensation Reconciliation / Payroll Period).
   - Phân biệt rõ giữa *'Sổ cái thu nhập'* (Earnings - tích lũy theo từng buổi dạy), *'Kỳ đối soát'* (Payroll Period - chốt theo chu kỳ), *'Nghĩa vụ chi trả'* (Payout Obligation - lệnh chi tiền phát sinh sau khi duyệt), và *'Tài khoản thụ hưởng'* (Masked Bank Account).
3. **Khuyến nghị duy trì:**
   - Khi thêm mới endpoint, lập trình viên nên viết trực tiếp XML comment `<summary>` và `<remarks>` ngay trên Action. Bộ lọc `CanonicalApiOperationFilter` sẽ ưu tiên giữ nguyên XML comment đó nếu hợp lệ.
   - Bộ test `SwaggerDocumentationQualityTests` sẽ tự động chạy trong CI/CD để ngăn chặn việc tái xuất hiện các câu placeholder chung chung.