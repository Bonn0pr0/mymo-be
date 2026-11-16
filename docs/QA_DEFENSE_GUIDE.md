# Tài liệu Câu hỏi & Đáp án Bảo vệ Đồ án (Q&A Defense Guide) – MyMo Backend

Tài liệu này tổng hợp **10 câu hỏi chuyên môn** thường gặp nhất khi hội đồng/giảng viên chấm đồ án kiến trúc **Microservices & 3-Layer (.NET 8)** cùng câu trả lời dẫn chứng trực tiếp vào source code của dự án.

---

### Câu 1: Tại sao dự án vừa sử dụng RabbitMQ lại vừa sử dụng Apache Kafka? Hai công nghệ này đóng vai trò khác nhau như thế nào trong MyMo?
* **Trả lời**:
  Dự án áp dụng mô hình **Hybrid Messaging** tách biệt rõ ràng giữa **Command (Mệnh lệnh)** và **Event (Sự kiện)** trong `src/Mymo.Shared/Messaging/Contracts.cs`:
  1. **RabbitMQ (`ICommandBus` – `RabbitCommandBus.cs`)**: Dùng cho các **Command điểm-tới-điểm (Point-to-Point Queue)** đòi hỏi một service cụ thể phải thực thi tác vụ thay đổi trạng thái có độ tin cậy cao (`BasicAck` / `BasicNack`). Gồm 4 queues:
     - `identity.commands.set-role`: Khi Admin duyệt quán bên `Business Service`, bắn lệnh sang `Identity Service` để nâng quyền User thành `"Business"`.
     - `identity.commands.set-premium`: Khi Admin xác nhận đơn thanh toán VietQR bên `Business Service`, bắn lệnh sang `Identity Service` để bật `IsPremium = true`.
     - `identity.commands.create-report`: Khi User báo cáo bài viết bên `Social Service`, bắn lệnh sang `Identity Service` lưu vào bảng `Reports`.
     - `map.commands.update-place-ad`: Khi chủ quán thanh toán chiến dịch quảng cáo bên `Business Service`, bắn lệnh sang `Map Service` gắn `AdPackageId` cho địa điểm.
  2. **Apache Kafka (`IEventBus` – `KafkaEventBus.cs`)**: Dùng cho **Event Streaming (Pub/Sub)** với lưu lượng cao. Khi người dùng đăng bài viết mới (`PostService.Create`), `Social Service` phát sự kiện `PostCreatedEvent` lên topic `mymo.social.post-created`. `Map Service` lắng nghe topic này (`KafkaConsumerHostedService`) và lập tức đẩy sự kiện `MapPostCreated` qua SignalR `MapHub` xuống tất cả người dùng đang mở bản đồ.

---

### Câu 2: YARP API Gateway đóng vai trò gì? Việc xác thực JWT được thực hiện ở đâu?
* **Trả lời**:
  - **YARP (`Mymo.Gateway` – Port `5079`)** đóng vai trò là **Single Entry Point (Reverse Proxy)** định tuyến toàn bộ HTTP REST requests và kết nối WebSockets (`/hubs/chat`, `/hubs/map`) về 5 Microservices phía sau dựa trên cấu hình `Routes` và `Clusters` trong `src/Mymo.Gateway/appsettings.json`, đồng thời gom toàn bộ tài liệu OpenAPI về một cổng Swagger UI duy nhất (`/swagger`).
  - **Xác thực JWT (`JwtBearer`)** được cấu hình thống nhất trong thư viện dùng chung `MymoService.Add` (`src/Mymo.Shared/Mymo.cs`). Cả 5 Microservices đều gọi `MymoService.Add` và tự xác thực chữ ký HMAC-SHA256 của JWT Token cũng như phân quyền theo Role (`RequireRole("Admin")`) ở từng endpoint.

---

### Câu 3: Khi kết nối Realtime qua SignalR (`/hubs/chat`, `/hubs/map`), trình duyệt không gửi được Header `Authorization: Bearer` trong WebSocket handshake chuẩn. Dự án xử lý việc này như thế nào?
* **Trả lời**:
  Trong file `src/Mymo.Shared/Mymo.cs` (dòng 187–197), sự kiện `JwtBearerEvents.OnMessageReceived` đã được cấu hình để đọc token từ query string `access_token` khi đường dẫn bắt đầu bằng `/hubs`:
  ```csharp
  var accessToken = context.Request.Query["access_token"];
  var path = context.HttpContext.Request.Path;
  if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
      context.Token = accessToken;
  ```
  Nhờ đó các Hub có gắn `[Authorize]` (`ChatHub`, `MapHub`) vẫn trích xuất được `Context.User` đầy đủ.

---

### Câu 4: Các Microservices có Database riêng biệt (không thể JOIN bảng trực tiếp). Vậy khi hiển thị bài viết hoặc phòng chat, làm sao lấy được thông tin User hoặc Địa điểm?
* **Trả lời**:
  Hệ thống kết hợp 2 kỹ thuật:
  1. **Synchronous Internal HTTP Call (`Internal.GetAsync` / `Internal.PostAsync`)**: Gọi các endpoint nội bộ `/internal/*` (ví dụ `POST /internal/users/batch` của `Identity Service` hoặc `GET /internal/places/{id}` của `Map Service`). Các endpoint này được bảo vệ bởi Header bí mật `X-Internal-Key` thông qua hàm `Internal.Allowed(request, config)`.
  2. **Denormalization (Snapshot dữ liệu)**: Khi tạo `PostRow`, `CommentRow`, `ReviewRow`, `MessageRow`, hệ thống lưu kèm bản chụp `DisplayName`, `Username`, `AvatarUrl` ngay tại thời điểm tạo để tối ưu tốc độ đọc Newsfeed mà không phải gọi sang `Identity Service` ở mỗi lần tải trang.

---

### Câu 5: Kiến trúc 3-Layer (`.Api`, `.Bll`, `.Dal`) trong từng Microservice giúp ích gì cho việc kiểm thử (Unit Testing)?
* **Trả lời**:
  - Tầng `.Bll` không phụ thuộc vào HTTP Context của ASP.NET Core (chỉ nhận `ClaimsPrincipal` và trả về `ServiceResult`) và cũng không phụ thuộc cứng vào cơ sở dữ liệu vật lý mà giao tiếp qua interface `I*Repository`, `ICommandBus`, `IEventBus`, `IChatRealtime`, `IMapRealtime`.
  - Nhờ đó trong project `tests/Mymo.Tests`, ta có thể truyền SQLite In-Memory (`Data Source=:memory:`) và các đối tượng Mock/Fake (`FakeCommandBus`, `FakeEventBus`, `FakeChatRealtime`) để kiểm thử độc lập toàn bộ logic nghiệp vụ của cả 5 Microservices.

---

### Câu 6: Hệ thống tính khoảng cách địa lý (tìm quán quanh đây, tìm bạn bè online quanh đây, bản đồ nhiệt) bằng thuật toán nào?
* **Trả lời**:
  Sử dụng công thức **Haversine (`Geo.Km` trong `src/Mymo.Shared/Mymo.cs`)** tính khoảng cách đường chim bay trên mặt cầu Trái Đất ($R = 6371\text{ km}$) từ vĩ độ (`Latitude`) và kinh độ (`Longitude`) giữa hai điểm.

---

### Câu 7: Quy trình thanh toán nâng cấp tài khoản Premium (B2C) và Chiến dịch Quảng cáo (B2B) hoạt động như thế nào?
* **Trả lời**:
  1. Khi User tạo đơn Premium (`POST /api/users/premium/orders`) hoặc Chủ quán tạo Campaign (`POST /api/business/campaigns`), `BusinessService` tạo mã tham chiếu duy nhất (`MPxxxxxx` hoặc `MMxxxxxx`) và sinh động đường dẫn ảnh mã QR ngân hàng theo chuẩn **VietQR** (`https://img.vietqr.io/image/{bin}-{account}-compact.png?amount={amount}&addInfo={reference}`).
  2. Người dùng quét mã chuyển khoản và bấm xác nhận (`ack` $\rightarrow$ trạng thái `Submitted`).
  3. Khi Admin bấm duyệt thanh toán (`ConfirmPremium` / `PayCampaign`), `BusinessService` chuyển trạng thái sang `Success` và bắn Command qua **RabbitMQ** để `Identity Service` bật `IsPremium = true` hoặc `Map Service` gắn `AdPackageId` cho quán.

---

### Câu 8: Làm sao hệ thống vừa hỗ trợ chạy SQLite (file `.db` local) vừa hỗ trợ chạy SQL Server trong Docker?
* **Trả lời**:
  Trong các lớp `*DalExtensions.cs` (ví dụ `IdentityDalExtensions.cs`), hàm `IsSqlServer(connectionString)` tự động kiểm tra chuỗi kết nối:
  - Nếu chuỗi kết nối chứa `Server=`, `1433`, hoặc `User Id=`, EF Core tự động cấu hình `options.UseSqlServer(...)` kèm cơ chế `EnableRetryOnFailure`.
  - Nếu chuỗi kết nối là `Data Source=...db`, EF Core tự động chuyển sang `options.UseSqlite(...)`.
