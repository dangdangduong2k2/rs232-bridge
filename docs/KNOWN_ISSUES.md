# Trạng thái lỗi — 0.5 RC

## Đã sửa trong source và bộ cài 0.5

- Thiếu Set/Get tần số: đã thêm handler band và working frequency, quy đổi kênh theo MHz.
- EPC speed bị chặn: đã nối extended profile ZK và kiểm tra Get sau Set. Một số mục dùng preset tương ứng, xem [RADIO_MAPPING.md](RADIO_MAPPING.md).
- Công suất chỉ đặt chung: đã dùng vector từng anten, cho phép yêu cầu một phần.
- Baseband/reporting từ chối `FF 00`: đã xử lý persistence; kiểm thử bằng DLL Nation gốc đạt.
- Baseband/reporting mất sau reconnect: state giữ qua phiên, chế độ lưu ghi file atomic. Kiểm thử restart đối tượng lưu trữ và reconnect TCP đạt.
- Setup COM bị chiếm: kiểm tra cổng Nation trước khi dừng dịch vụ/thay payload; yêu cầu đóng Nation nếu cổng còn bận.

## Cần nghiệm thu thêm

- Reconnect COM nhanh: đã bỏ thao tác xóa buffer có thể làm mất request đầu tiên, nhưng cần thử lại trên cặp COM ảo thật. Reconnect TCP 100/350/700 ms đạt không thay thế kiểm thử COM.
- Chưa nghiệm thu toàn bộ cấu hình mới của bridge 0.5 trên module thật; Nation đang giữ COM nên đợt phát hành này dùng kiểm thử mô phỏng/SDK. Kết quả API riêng không được tính là nghiệm thu bridge.
- Chưa kiểm tra lưu RF qua mất nguồn, module 1 anten, tháo/cắm USB và chạy dài hạn cho 0.5.

## Giới hạn có chủ đích

- EPC speed là preset tương thích theo menu Nation v0.39, không bảo đảm Tari/BLF trùng ở mọi mục. Auto dùng preset cố định. Chỉ firmware có extended profile được hỗ trợ ở phần này.
- Không hỗ trợ band Nation hai dải rời, danh sách kênh không liên tiếp hoặc dò kênh tự động. Không giả lập thành công cho tùy chọn không có ánh xạ.
- SuperRW, Lock/Kill, GPIO, BlockWrite, firmware và toàn bộ các lệnh SDK ngoài bảng handler vẫn chưa hỗ trợ.
- RF anten 3–4 chưa nghiệm thu đọc thẻ trong đợt trước.

Xem [VALIDATION.md](VALIDATION.md) để phân biệt kiểm thử mới, kiểm thử cũ và phần chưa kiểm chứng.
