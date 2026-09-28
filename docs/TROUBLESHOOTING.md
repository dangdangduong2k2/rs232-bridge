# Xử lý lỗi

## Kiểm tra nhanh

1. Module có nguồn và COM vật lý có mặt trong Device Manager.
2. Nation chọn **Nation COM Port**, baud 115200; không chọn cổng nội bộ hoặc cổng module.
3. Chỉ một ứng dụng dùng cổng; đóng demo ZK và serial terminal.
4. Kiểm tra dịch vụ, trạng thái và log bằng PowerShell:

```powershell
Get-Service NationZkComBridge
Get-Content "$env:ProgramData\NationComPort\status.txt"
Get-Content "$env:ProgramData\NationComPort\setup.log" -Tail 80
Get-Content "$env:ProgramData\NationComPort\logs\service.log" -Tail 100
```

File chỉ có sau khi thành phần tương ứng đã chạy. `status.txt` báo worker sẵn sàng không có nghĩa tất cả chức năng phần cứng đã đạt.

## Lỗi thường gặp

| Hiện tượng | Kiểm tra / xử lý |
|---|---|
| `Access to the port 'COMx' is denied` lúc setup | Cổng có thể đang bị Nation hoặc chương trình khác giữ. Đóng hoàn toàn phần mềm dùng cổng rồi chạy setup lại. Nếu vẫn lỗi, kiểm tra log và trạng thái driver. |
| Không thấy COM vật lý | Kiểm tra nguồn, cáp và driver USB/RS232 của adapter. Bridge không tạo thay COM vật lý. |
| Windows chưa cấp COM, driver có dấu chấm than/Code 52 | Ghi mã lỗi trong Device Manager, kiểm tra tải CAB và log setup. Không tự bật test-signing hoặc tắt Secure Boot để chạy bản này. |
| Tải CAB lỗi / SHA256 không khớp | Kiểm tra Internet/proxy. Bộ cài dừng để tránh dùng gói khác với bản đã chốt; không bỏ kiểm tra hash. |
| Máy đã có com0com | Bộ cài không tự thay driver/cặp cổng của ứng dụng khác. Cần kỹ thuật viên kiểm tra cài đặt hiện có. |
| `pending-pair.txt` sau cài gián đoạn | Giữ log và file này để đối chiếu cặp COM đã tạo; không chạy lặp hoặc xóa dấu trạng thái tùy ý. |
| Không tìm được thiết bị đã lưu | Cắm đúng module/adapter ban đầu. Muốn đổi thiết bị hoặc baud/số anten, gỡ và cài lại. |
| Có COM nhưng không kết nối | Kiểm tra baud module, COM vật lý, dịch vụ và ứng dụng đang chiếm cổng. Đóng kết nối, đợi 1–2 giây rồi mở lại. |
| Kết nối được nhưng không đọc thẻ | Kiểm tra 6C, anten được chọn, cáp antenna, thẻ, vùng đọc, công suất và bộ lọc. |
| Ghi vùng nào cũng lỗi | Stop inventory; dùng Write 6C thay vì SuperRW; kiểm tra chọn thẻ, anten, password, word address/length và dữ liệu hex. Xem log `UNSUPPORTED` hoặc lỗi backend. |
| Đặt công suất lỗi | Gửi đủ các anten của module với cùng công suất trong khoảng cho phép, sau khi Stop. |
| Baseband/tag reporting lỗi hoặc mất sau reconnect | Đây là lỗi đã biết của 0.4 RC; xem [danh sách lỗi](KNOWN_ISSUES.md). |

## Kiểm tra đường truyền bằng DLL Nation gốc

Đóng Nation trước khi chạy. Thay COM52 bằng **Nation COM Port** thực tế:

```powershell
& "$env:ProgramFiles\NationComPort\diagnostics\NationSerialCheck.exe" --serial COM52:115200
```

Helper chỉ đọc thông tin reader và capabilities, không inventory RF hoặc ghi thẻ. `PASS` xác nhận bước kiểm tra này; không thay thế kiểm tra ghi/đọc và các chức năng khác.

## Nội dung cần gửi khi báo lỗi

- Phiên bản release, phiên bản Windows và kiến trúc máy.
- Model/firmware module, số anten, baud, tên ba cổng COM.
- Các bước tái hiện, mã lỗi, thời gian xảy ra, có đang inventory hay không.
- Đoạn `setup.log`/`service.log` gần thời điểm lỗi và kết quả helper nếu có.

Kiểm tra nội dung trước khi đăng issue public: che định danh thiết bị, dữ liệu thẻ, đường dẫn cá nhân và thông tin nhạy cảm. Payload ghi được che ở log RX nhưng log khác vẫn có thể chứa định danh hoặc dữ liệu vận hành.
