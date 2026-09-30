# Changelog

## 0.5 RC4 — 2026-09-30

- Sửa inventory chậm/mất lượt: đọc EPC và PC trong cùng lệnh mixed inventory, chỉ đọc PC riêng khi thiếu. Giữ từng lượt thẻ, không tạo PC hoặc nhân count giả.
- Kiểm tra sequence của packet EPC/PC; hỗ trợ wrap 127→0, phase extension và dữ liệu partial status 4. Firmware từ chối rõ ràng mới fallback về cách đọc cũ.
- Thêm log tổng số báo cáo gửi/lọc và lỗi PC để chẩn đoán tốc độ, tránh log mã EPC khi đọc PC lỗi.
- Sửa giải nén thư mục ZIP kết thúc bằng dấu `\` do .NET Framework tạo, gây lỗi `$PLUGINSDIR` trong bộ cài.
- Đo trên reader thật: backend từ 2.4–2.8 lên 53.2–53.3 lượt/giây; qua SDK Nation/TCP ~34.7 lượt/giây ở lượt thử riêng. Chi tiết và giới hạn trong `docs/INVENTORY_PERFORMANCE.md`.

## 0.5 RC3 — 2026-09-28

- Sửa Q/Session chỉ lưu ở bridge: Set/Get nay dùng CFG9 thật của Ex10, đọc lại sau Set và trước inventory.
- Get đầu phiên không phụ thuộc Set hoặc Q/Session trong file của bridge; phản ánh thay đổi từ phần mềm ZK. Thêm regression cho Get đầu tiên, thay đổi bên ngoài, cập nhật từng trường và reconnect.
- Kiểm chứng độc lập trên COM49: SDK Nation Get đầu tiên với timeout mặc định, Q=0/15, Session=0..3 và đọc ngược qua SDK ZK; khôi phục Q=6, Session=1. Không ghi thẻ.

## 0.5 RC2 — 2026-09-28

- Đã cài và kiểm chứng trên COM thật: mode 5 và đủ 13 EPC speed, 7 band, từng anten, reconnect 100/350/700 ms.
- Bỏ band 12 bị module thật từ chối; dịch lỗi tham số/profile ZK thành không hỗ trợ thay vì lỗi lưu.
- Giữ giới hạn firmware và khác biệt preset được mô tả trong RADIO_MAPPING.md.


## 0.5 RC — 2026-09-28

- Set/Get công suất vector qua GetAntennaPower/SetAntennaPower; cho phép sửa riêng một anten.
- Thêm Set/Get band và working frequency, xử lý offset kênh theo MHz.
- EPC speed chuyển sang extended profile Ex10; đọc lại độc lập sau Set.
- Nhận persistence FF00 của baseband/reporting; giữ cấu hình qua reconnect và lưu atomic cho chế độ persistence.
- Không xóa buffer chứa request đầu tiên của phiên COM mới sau khi đóng backend cũ.
- Bổ sung kiểm thử giả lập lỗi/readback, DLL Nation gốc và tài liệu bảng quy đổi.


## v0.4.0-rc.1 — 2026-09-28

Bản đầu tiên đóng gói source và EXE để chia sẻ qua GitHub, dựa trên bridge 0.4 đã thử phần cứng ngày 25/09/2026.

- Bridge giao thức RS232 cho phần mềm Nation dùng module ZK, giữ DLL Nation nguyên bản.
- Bộ cài gộp worker, dịch vụ và công cụ COM; tải driver từ Microsoft Update Catalog có kiểm tra SHA256.
- Giao diện chính tối giản: `driver RS232 brigde`, `setup`, `cancel`; chọn phần cứng lần đầu.
- Source build, test tự động, workflow Windows, checksum EXE và tài liệu cài/vận hành/phát triển.
- Đọc EPC/TID/User/Reserved, ghi EPC/User và đặt công suất chung đã có kiểm chứng trong phạm vi [VALIDATION.md](docs/VALIDATION.md).

Đây là prerelease. Bốn lỗi reconnect/persistence và các tính năng chưa hỗ trợ vẫn còn; xem [KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md). Việc công bố và rút gọn UI không sửa các lỗi giao thức này.
