# Cách sử dụng

## Kết nối

Sau khi cài thành công, mở phần mềm Nation như trước, chọn **RS232 → Nation COM Port → 115200**. Giữ DLL Nation gốc trong thư mục ứng dụng. Cổng vật lý ZK và `Nation Bridge Internal` dành cho bridge.

Chỉ một ứng dụng mở mỗi cổng tại một thời điểm. Khi đóng rồi mở lại kết nối, đợi 1–2 giây để tránh lỗi reconnect đã biết. Sau khởi động Windows, chờ dịch vụ sẵn sàng rồi kết nối.

## Đọc thẻ

1. Chọn giao thức **6C**, anten có nối antenna thực tế và đặt thẻ vào vùng đọc.
2. Chọn đọc một lần hoặc inventory liên tục, bấm Start theo giao diện Nation.
3. Có thể yêu cầu dữ liệu EPC/TID/User/Reserved khi thẻ hỗ trợ vùng nhớ tương ứng.
4. Bấm Stop trước khi đổi cấu hình hoặc ghi thẻ.

Bản 4 anten đã đọc RF thành công trên anten 1–2. Anten 3–4 và bản 1 anten cần kiểm chứng trên thiết bị thực tế. Có tùy chọn trong giao diện không đồng nghĩa mọi lệnh Nation đều được bridge hỗ trợ.

## Ghi thẻ

- Dùng chức năng ghi 6C thông thường của Nation (lệnh `0x211`), không dùng **SuperRW**.
- Dừng inventory. Chọn anten và thẻ đích bằng bộ lọc EPC/TID; nên dùng TID khi đang thay EPC để còn tìm được thẻ sau khi đổi.
- Nhập đúng vùng nhớ, địa chỉ word, dữ liệu hex và access password của thẻ. Một word là 2 byte; lệnh hỗ trợ 1–64 word trong phạm vi thẻ cho phép.
- Ghi rồi đọc lại vùng nhớ để xác nhận dữ liệu. EPC và User đã được thử trên thẻ thật; ghi mật khẩu/Reserved và các vùng khác chưa nghiệm thu.
- Thẻ bị khóa, không có vùng User hoặc mật khẩu sai có thể từ chối thao tác. TID thường không ghi được tùy loại thẻ; việc đọc được TID không bảo đảm ghi được.

Để thử ban đầu, đặt một thẻ thử trong vùng đọc và lưu dữ liệu gốc. Bridge chưa hỗ trợ Lock/Kill/BlockWrite/SuperRW.

## Cài công suất

Có hỗ trợ đọc và đặt công suất qua giao diện Nation. Dừng inventory trước khi đặt. Từ 0.5 RC, backend dùng vector công suất từng anten:

- Module 4 anten: chọn một hoặc nhiều ANT1–ANT4; mỗi anten có thể đặt mức riêng.
- Module 1 anten: gửi ANT1.
- Anten không được chọn giữ nguyên công suất hiện tại. Anten ngoài số cổng cấu hình bị từ chối.

Chỉ chọn trong khoảng capabilities trả về; không tự suy ra giới hạn từ giao diện Nation. Đã xác nhận đổi tạm thời 22 → 21 → 22 dBm và đọc lại đúng. Khả năng lưu qua mất nguồn chưa nghiệm thu.

## Bộ lọc và cấu hình phiên

Lọc EPC/TID, chống trùng và ngưỡng RSSI có đường xử lý. Chống trùng dùng đơn vị 10 ms trong giao thức. Từ 0.5 RC, Baseband và tag reporting nhận `IsPersistence=0`, giữ qua reconnect và có lưu file cho chế độ persistence. Xem [RADIO_MAPPING.md](RADIO_MAPPING.md) về khác biệt giữa lưu trên module và lưu trên máy bridge.

Nếu gặp lỗi, ghi lại thao tác, thời gian, mã lỗi và đọc [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Cấu hình RF từ 0.5 RC

Chọn anten cần sửa, chọn công suất rồi Set; Get đọc riêng từng anten. EPC Speed và Frequency Range/F-hop đã có handler. Xem [RADIO_MAPPING.md](RADIO_MAPPING.md) trước khi đối chiếu thông số RF: cùng nhãn preset không có nghĩa Tari/BLF hai hãng giống hoàn toàn.
