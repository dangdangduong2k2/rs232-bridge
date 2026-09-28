# Kiểm chứng 0.5 RC — 28/09/2026

## RC3: kiểm chứng Q/Session trên module thật

- Module UHF7182M type 0x75, firmware 2.8, COM49/115200. `GetQS` cũ trả 0xEE; `GetCfgParameter(9)` trả `06-01` (Q=6, Session=1). Bridge RC2 trước sửa đang trả Q=0 từ state cũ.
- Bài mới `BasebandSdk`: **38 assertion qua TCP**, rồi **38 assertion qua COM52 và dịch vụ RC3 đã cài**. Dùng DLL Nation gốc, timeout mặc định của `SendSynMsg`.
- Get là lệnh đầu tiên sau kết nối, trước mọi Set; đọc đúng CFG9 mà không cần khởi tạo state. Đặt CFG9 từ SDK ZK, đóng cổng rồi Get qua Nation đọc đúng thay đổi.
- Set Q riêng/Session riêng; Q=0 và 15, Session=0..3; đóng Nation rồi đọc độc lập qua SDK ZK xác nhận cặp đã được thay đổi thật. Ba lần reconnect, Get đầu phiên đều đạt.
- Khôi phục và đọc lại Q=6, Session=1. Tất cả Set kiểm thử là tạm; không đổi profile/tần số/công suất, không inventory hoặc ghi thẻ.
- RC3 local suite: 24 packaging, 18 protocol, 60 radio, 39 write; tích hợp SDK mô phỏng và RadioSdk 170 assertion đạt. Hash worker đã cài khớp build.
- Chưa power-cycle để nghiệm thu flash. Chưa tái hiện độc lập nguyên nhân lỗi giao diện Get đầu tiên của người dùng; regression SDK nêu trên đã đạt.

## Kiểm thử mới

Trên Windows x64/.NET Framework bằng source và EXE vừa build:

- 24 kiểm tra đóng gói; 18 protocol/state; 39 write; 52 kiểm tra cấu hình RF/lưu trạng thái/lỗi readback.
- Tích hợp DLL Nation nguyên bản kiểm tra đọc/ghi/callback mô phỏng, power vector và cập nhật riêng anten (số assertion callback phụ thuộc lịch chạy).
- `RadioSdk`: 170 assertion qua DLL Nation nguyên bản, 7 band, fixed/auto frequency, 13 mục EPC speed, công suất riêng từng anten, reconnect TCP 100/350/700 ms, khôi phục trạng thái kiểm thử.
- Test file-state kiểm tra lưu chọn lọc, không ghi lẫn thay đổi tạm, tải lại từ file và lỗi ghi file không báo thành công.
- Self-extraction kiểm tra SHA256 toàn bộ payload; dừng worker và diagnostic Nation đạt.
- Các test trong `test.ps1` dùng backend mô phỏng; không cài driver, không đọc/ghi thẻ thật.

## Phần cứng 0.5 RC2 — 28/09/2026

- Module ZK type 0x75, firmware 2.8, 4 anten, COM49/115200.
- Cùng bài `RadioSdk` đạt 170 assertion qua TCP frontend tới module thật, sau đó đạt 170 assertion qua **DLL Nation nguyên bản → COM52 → dịch vụ đã cài → COM49**.
- 13 mục EPC speed: 0–7, 10–13, Auto; mode 5 Set/Get đúng ZK extended profile 103 (FM0 640 kHz, Tari 6.25 µs).
- 7 band Nation 0/1/3/4/5/13/15; Get/Set band, kênh cố định và auto đã đọc lại đúng.
- Power `[8,7,6,5]`, cập nhật riêng ANT1 và giữ các anten còn lại; đọc lại đúng.
- Reconnect COM với khoảng nghỉ 100/350/700 ms (sáu lượt) không mất query đầu; Q/session/reporting giữ nguyên.
- Khôi phục `[8,8,8,8]`, band 3/kênh 0–49, profile 146 (preset Nation 1), Q=4/session=0/target=2, reporting=0; kiểm tra lại đạt.
- Mọi Set trong bài này là tạm thời. Không inventory RF, không ghi dữ liệu thẻ và không power-cycle. Không suy ra khả năng đọc thẻ/s ở từng profile chỉ từ Set/Get.
- Lần chạy đầu phát hiện Nation band 12 → ZK band 3 bị module trả 0xFF; RC2 bỏ ánh xạ đó và sửa mã lỗi phản hồi. Lần đầu đã khôi phục trạng thái trước khi tiếp tục.
- Bộ cài chạy thành công, worker đã cài có SHA256 khớp bản build, dịch vụ Running. File EXE Nation và GReaderApi.dll không sửa.

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
