# Kiểm chứng 0.5 RC — 28/09/2026

## Kiểm thử mới

Trên Windows x64/.NET Framework bằng source và EXE vừa build:

- 24 kiểm tra đóng gói; 18 protocol/state; 39 write; 52 kiểm tra cấu hình RF/lưu trạng thái/lỗi readback.
- Tích hợp DLL Nation nguyên bản kiểm tra đọc/ghi/callback mô phỏng, power vector và cập nhật riêng anten (số assertion callback phụ thuộc lịch chạy).
- `RadioSdk`: 179 assertion qua DLL Nation nguyên bản, 8 band, fixed/auto frequency, 13 mục EPC speed, công suất riêng từng anten, reconnect TCP 100/350/700 ms, khôi phục trạng thái kiểm thử.
- Test file-state kiểm tra lưu chọn lọc, không ghi lẫn thay đổi tạm, tải lại từ file và lỗi ghi file không báo thành công.
- Self-extraction kiểm tra SHA256 toàn bộ payload; dừng worker và diagnostic Nation đạt.
- Các test trong `test.ps1` dùng backend mô phỏng; không cài driver, không đọc/ghi thẻ thật.

## Phần cứng cho 0.5

Chưa hoàn tất vì phần mềm Nation đang giữ COM49 qua dịch vụ cũ. Chưa thay worker đang cài, chưa tuyên bố các profile/band mới đều được firmware thật chấp nhận. `bridge-tests/RadioSdk.cs` được chuẩn bị để thử cấu hình tạm qua TCP hoặc COM; không tự chạy trên phần cứng trong `test.ps1`.

Bài kiểm thử phần cứng riêng của `zk-reader-api` đã xác nhận lệnh ZK cho power vector, band và extended profile trên COM49. Nó là bằng chứng cho lệnh ZK, không thay thế nghiệm thu đường Nation → bridge → ZK 0.5.

GitHub Actions từng bị chặn bởi billing tài khoản. Kết quả nêu trên là kiểm thử local, không coi workflow tồn tại là CI đã đạt.

---

# Kết quả 0.4 RC trước đây

## Trên phần cứng thật

Windows x64, ZK 4 anten firmware 2.8/type 0x75, kết nối vật lý 115200; phần mềm dùng DLL Nation nguyên bản qua COM ảo.

- OpenSerial, thông tin reader, capabilities, đọc một lần/liên tục, Stop, lọc EPC/TID đạt.
- Anten 1–2 đọc được thẻ; anten 3–4 chưa có thẻ trong vùng thử nên chưa nghiệm thu RF.
- Đọc EPC/TID/User/Reserved đạt. Ghi EPC và User đã đọc lại đúng và khôi phục dữ liệu cũ; không thử ghi mật khẩu, lock hoặc kill.
- Công suất tạm thời 22→21→22 dBm đã đọc lại đúng; chống trùng 1 giây và lọc RSSI đạt trong phiên hiện tại.
- CRC sai, frame chia nhỏ/gộp, heartbeat được kiểm tra qua COM thật.
- Phát hiện 4 lỗi reconnect/persistence; trạng thái sửa ở [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

## Tự động

`build.ps1`: 24 kiểm tra cấu hình và đóng gói.

`test.ps1`: 18 kiểm tra giao thức, 39 kiểm tra Write, khoảng 30 assertion tích hợp DLL Nation (số assertion callback phụ thuộc lịch chạy), kiểm tra dừng worker, helper Nation và giải nén payload khớp hash.

Đợt audit riêng chạy 5.000 frame dữ liệu sai/ngẫu nhiên qua backend giả không có exception thoát ra ngoài. Các kết quả mô phỏng không thay thế nghiệm thu đầy đủ trên mọi model/Windows.

EXE có giao diện tối giản: `driver RS232 brigde`, `setup`, `cancel`. Giao diện đã được kiểm tra trực tiếp sau build.
