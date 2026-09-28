# Kiểm chứng 0.4 RC

## Trên phần cứng thật

Windows x64, ZK 4 anten firmware 2.8/type 0x75, kết nối vật lý 115200; phần mềm dùng DLL Nation nguyên bản qua COM ảo.

- OpenSerial, thông tin reader, capabilities, đọc một lần/liên tục, Stop, lọc EPC/TID đạt.
- Anten 1–2 đọc được thẻ; anten 3–4 chưa có thẻ trong vùng thử nên chưa nghiệm thu RF.
- Đọc EPC/TID/User/Reserved đạt. Ghi EPC và User đã đọc lại đúng và khôi phục dữ liệu cũ; không thử ghi mật khẩu, lock hoặc kill.
- Công suất tạm thời 22→21→22 dBm đã đọc lại đúng; chống trùng 1 giây và lọc RSSI đạt trong phiên hiện tại.
- CRC sai, frame chia nhỏ/gộp, heartbeat được kiểm tra qua COM thật.
- Phát hiện 4 lỗi còn mở tại [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

## Tự động

`build.ps1`: 24 kiểm tra cấu hình và đóng gói.

`test.ps1`: 18 kiểm tra giao thức, 39 kiểm tra Write, khoảng 30 assertion tích hợp DLL Nation (số assertion callback phụ thuộc lịch chạy), kiểm tra dừng worker, helper Nation và giải nén payload khớp hash.

Đợt audit riêng chạy 5.000 frame dữ liệu sai/ngẫu nhiên qua backend giả không có exception thoát ra ngoài. Các kết quả mô phỏng không thay thế nghiệm thu đầy đủ trên mọi model/Windows.

EXE có giao diện tối giản: `driver RS232 brigde`, `setup`, `cancel`. Giao diện đã được kiểm tra trực tiếp sau build.
