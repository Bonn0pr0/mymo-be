# Thiết kế Kiến trúc Lớp (Class Design) & Cơ sở dữ liệu (Database Design)

Tài liệu này mô tả chi tiết cấu trúc **3-Layer Architecture (`Api` – `Bll` – `Dal`)** và thiết kế **5 Cơ sở dữ liệu độc lập (28 bảng)** của hệ thống **MyMo Backend**.

---

## 1. Cấu trúc 3-Layer trong từng Microservice

Mỗi Microservice (`Identity`, `Social`, `Map`, `Chat`, `Business`) được chia thành 3 project độc lập:

1. **Tầng Presentation (`Mymo.<Service>.Api`)**:
   - Định nghĩa nhóm RESTful Endpoints (`IdentityEndpoints`, `SocialEndpoints`, `MapEndpoints`, `ChatEndpoints`, `BusinessEndpoints`).
   - Cung cấp SignalR Hubs (`ChatHub`, `MapHub`) và lớp thực thi Realtime (`ChatRealtime`, `MapRealtime`).
2. **Tầng Business Logic (`Mymo.<Service>.Bll`)**:
   - Chứa các lớp nghiệp vụ (`AuthService`, `UserService`, `AdminService`, `PostService`, `FriendService`, `NotificationService`, `MapService`, `ChatService`, `BusinessService`).
   - Phối hợp dữ liệu nội bộ qua `Internal` HTTP (`X-Internal-Key`) và phát lệnh/sự kiện qua `ICommandBus` (RabbitMQ) / `IEventBus` (Kafka).
3. **Tầng Data Access (`Mymo.<Service>.Dal`)**:
   - Đóng gói truy vấn Entity Framework Core 8 thông qua **Repository Pattern** (`IIdentityRepository`, `ISocialRepository`, `IMapRepository`, `IChatRepository`, `IBusinessRepository`).
   - Quản lý `DbContext` và cơ chế khởi tạo dữ liệu mẫu (`*Seed.Run`).

---

## 2. Thiết kế Cơ sở Dữ liệu Phân tán (Database-per-Service)

Hệ thống tách biệt thành **5 Databases** với tổng cộng **28 bảng**:

### 2.1. `mymo_identity.db` (Identity Service – 5 Bảng)
- `Users` (`AppUser`): Thông tin tài khoản, tọa độ hiện tại (`Latitude`, `Longitude`), trạng thái trực tuyến (`IsOnline`), phân quyền (`Role`, `Status`), gói `IsPremium` và cấu hình giao diện cá nhân (`MapMarker`, `AvatarFrame`, `ProfileTheme`, `ProfileTitle`).
- `Sessions` (`UserSession`): Quản lý phiên đăng nhập và `RefreshToken`.
- `Otps` (`OtpChallenge`): Lưu mã xác thực OTP 6 số cho đăng ký (`register`) và đặt lại mật khẩu (`reset`).
- `Reports` (`ReportRow`): Lưu danh sách báo cáo vi phạm từ người dùng gửi về qua RabbitMQ.
- `AuditLogs` (`AuditLogRow`): Ghi nhận lịch sử thay đổi trạng thái hệ thống của Admin.

### 2.2. `mymo_social.db` (Social Service – 7 Bảng)
- `Posts` (`PostRow`): Bài viết gắn tọa độ/địa điểm, hỗ trợ chế độ ẩn danh (`IsAnonymous`, `AnonymousAlias`) và thời gian hết hạn (`ExpiresAt`).
- `Media` (`MediaRow`): Lưu đường dẫn hình ảnh/video đính kèm bài viết.
- `Comments` (`CommentRow`): Bình luận bài viết, hỗ trợ trả lời đa cấp (`ParentCommentId`).
- `Likes` (`LikeRow`): Lượt thích bài viết (Unique Index trên `(PostId, UserId)`).
- `Views` (`PostViewRow`): Lịch sử người dùng đã xem bài viết.
- `Friendships` (`FriendshipRow`): Quan hệ kết bạn và chặn người dùng (`Pending`, `Accepted`, `Rejected`, `Blocked`).
- `Notifications` (`NotificationRow`): Thông báo tương tác (thích bài, lời mời kết bạn, đồng ý kết bạn).

### 2.3. `mymo_map.db` (Map Service – 6 Bảng)
- `Categories` (`PlaceCategoryRow`): Danh mục địa điểm (Cafe, Restaurant, Study Spot...).
- `Places` (`PlaceRow`): Thông tin quán/địa điểm, tọa độ GPS, thẻ cảm xúc (`Vibes`), điểm đánh giá trung bình (`AverageRating`) và mã gói quảng cáo (`AdPackageId`).
- `Photos` (`PlacePhotoRow`): Bộ sưu tập ảnh của địa điểm.
- `CheckIns` (`CheckInRow`): Lịch sử check-in kèm tâm trạng (`Mood`) và thời tiết (`Weather`).
- `Reviews` (`ReviewRow`): Đánh giá sao (1–5) và nhận xét của khách hàng.
- `Impressions` (`ImpressionRow`): Ghi nhận lượt hiển thị địa điểm trên bản đồ.

### 2.4. `mymo_chat.db` (Chat Service – 3 Bảng)
- `Conversations` (`ConversationRow`): Phòng trò chuyện cá nhân (`Private`), lưu tin nhắn cuối (`LastMessage`, `LastMessageTime`).
- `Members` (`MemberRow`): Thành viên trong cuộc trò chuyện và mốc thời gian đọc tin (`LastReadAt`).
- `Messages` (`MessageRow`): Tin nhắn văn bản, hình ảnh hoặc tọa độ vị trí.

### 2.5. `mymo_business.db` (Business Service – 7 Bảng)
- `Businesses` (`BusinessRow`): Hồ sơ doanh nghiệp/chủ quán và trạng thái xét duyệt (`Pending`, `Approved`, `Rejected`).
- `Menus` (`MenuRow`) & `MenuItems` (`MenuItemRow`): Thực đơn và danh sách món ăn/đồ uống kèm giá.
- `Promotions` (`PromotionRow`): Các chương trình ưu đãi của địa điểm.
- `Packages` (`PackageRow`): Danh mục gói sản phẩm B2C (`monthly`, `yearly`) và B2B (`starter_spot`, `featured_venue`, `event_boost`).
- `PremiumOrders` (`PremiumOrderRow`): Đơn hàng nâng cấp tài khoản Premium kèm mã chuyển khoản VietQR (`MPxxxxxx`).
- `Campaigns` (`CampaignRow`): Chiến dịch quảng cáo địa điểm kèm mã chuyển khoản VietQR (`MMxxxxxx`).

