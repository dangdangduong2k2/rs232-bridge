# Các lỗi đã biết — 0.4 RC

Các lỗi dưới đây đã được tái hiện trong đợt kiểm thử 25/09/2026 và **chưa được sửa trong EXE phát hành này**.

1. **Nối lại COM quá nhanh có thể mất phản hồi đầu tiên.** Khoảng nghỉ 350 ms đã tái hiện lỗi; các lượt 700–1500 ms đạt. Tạm thời đợi 1–2 giây trước khi nối lại.
2. **Baseband với `IsPersistence=0` bị từ chối.** Handler chưa xử lý PID `FF 00` của SDK.
3. **Tag reporting với `IsPersistence=0` bị từ chối.** Cũng thiếu xử lý PID `FF 00`.
4. **Tag reporting ở chế độ lưu báo thành công nhưng mất cấu hình sau reconnect.** Cấu hình hiện chỉ nằm trong phiên bridge. Không dựa vào khả năng lưu cấu hình này.

## Giới hạn

- Trong 190 lớp lệnh SDK được rà, 15 lớp có handler; 159 lớp có frame mặc định trả lỗi chưa hỗ trợ; 16 lớp cần tham số riêng chưa kiểm thử gửi lệnh. Có handler không có nghĩa mọi tùy chọn đã hỗ trợ.
- `MsgBaseSuperRW` (`0x21A`) khác Write EPC/User (`0x211`); SuperRW chưa hỗ trợ.
- Chưa hỗ trợ Lock/Kill, GPIO, BlockWrite, firmware hoặc toàn bộ tính năng mạng/cache của Nation.
- Anten 3–4 chưa nghiệm thu RF khi đặt thẻ đúng vùng; module 1 anten, reboot/hot-unplug, tải cao và chạy dài hạn chưa nghiệm thu.
- Nếu cập nhật báo Access denied COMx, đóng kết nối Nation trước khi chạy setup lại.
