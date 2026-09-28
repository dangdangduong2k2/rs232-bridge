# Cài đặt — 0.4 RC

## Yêu cầu

- Windows x64 Intel/AMD, .NET Framework 4.5 trở lên. Chưa hỗ trợ bộ cài trên Windows x86 hoặc ARM.
- Quyền quản trị để cài driver COM ảo và dịch vụ Windows.
- Internet để tải gói driver cố định từ Microsoft Update Catalog ở lần đầu; tải lỗi hoặc hash không khớp sẽ dừng cài.
- Reader ZK có giao thức tương thích `UHFReader288.dll`, xuất hiện dưới một COM vật lý và đã có driver USB/RS232 tương ứng.
- Biết baud của module và số anten 1 hoặc 4. Bản đã kiểm chứng phần cứng là module 4 anten, firmware 2.8/type 0x75, baud 115200. Không suy ra mọi model ZK đều tương thích.

Adapter USB phải hiện COM hoạt động trong Device Manager trước khi cài. Bộ cài bridge không kèm driver cho mọi loại adapter. Máy có com0com do ứng dụng khác quản lý cần kỹ thuật viên tích hợp; bộ cài không tự thay cặp cổng đó.

## Cài mới

1. Tải [RS232-Bridge-Setup.exe](https://github.com/dangdangduong2k2/rs232-bridge/releases/download/v0.4.0-rc.1/RS232-Bridge-Setup.exe).
2. Cắm module, xác định COM vật lý trong **Device Manager → Ports (COM & LPT)**.
3. Đóng Nation và mọi phần mềm ZK/serial terminal đang mở cổng.
4. Chạy EXE. Cửa sổ chính chỉ có `driver RS232 brigde`, `setup` và `cancel`.
5. Bấm **setup**, chọn COM vật lý, baud thực tế và số anten. Xác nhận quyền quản trị khi Windows yêu cầu.
6. Chờ thông báo hoàn tất, ghi lại số **Nation COM Port**. Nếu báo lỗi, xem [xử lý lỗi](TROUBLESHOOTING.md).
7. Mở Nation, chọn RS232, **Nation COM Port**, baud **115200**, rồi kết nối và đọc thử.

Ví dụ cổng vật lý COM49, Nation COM Port COM52, Nation Bridge Internal COM53: **phần mềm Nation chọn COM52**. Số COM trên máy khác có thể khác.

## Cài đặt tạo những gì?

| Thành phần | Vị trí / tên |
|---|---|
| File chương trình, DLL, cấu hình | `%ProgramFiles%\NationComPort` |
| Cấu hình thiết bị | `%ProgramFiles%\NationComPort\settings.xml` |
| Dịch vụ tự chạy cùng Windows | `NationZkComBridge` |
| Cổng cho ứng dụng | `Nation COM Port (COMx)` |
| Cổng nội bộ | `Nation Bridge Internal (COMy)` |
| Nhật ký và trạng thái | `%ProgramData%\NationComPort` |

Bộ cài kiểm tra truyền hai chiều của cặp COM, trạng thái mở/đóng và phản hồi thông tin reader bằng SDK Nation trước khi báo thành công. Đây không phải nghiệm thu mọi chức năng RF.

## Nâng cấp / cài lại

Đóng Nation và các chương trình dùng COM, chạy EXE mới rồi bấm **setup**. Cài đặt hiện có sử dụng lại cấu hình thiết bị đã lưu. Không đổi COM vật lý bằng cách chọn đầu COM ảo.

Để đổi module, baud hoặc số anten, gỡ bản đã cài rồi cài lại. Cấu hình lưu định danh thiết bị; đổi số COM của cùng thiết bị có thể được dịch vụ nhận diện lại, nhưng hot-unplug và reboot chưa được nghiệm thu đầy đủ.

## Gỡ cài đặt

Đóng Nation. Mở danh sách ứng dụng đã cài trong Windows, chọn **Nation COM Port (ZK bridge)** và gỡ. Kỹ thuật viên cũng có thể chạy:

```powershell
& "$env:ProgramFiles\NationComPort\NationComPortSetup.exe" /uninstall
```

Quá trình gỡ kiểm tra đúng cặp COM do bộ cài quản lý, dừng/xóa dịch vụ và xóa cặp cổng đó. Driver trong Driver Store, file chẩn đoán và log được giữ lại. Cài lỗi chưa có cấu hình hoàn tất cần kiểm tra `setup.log`/`pending-pair.txt`; không xóa cặp COM dùng chung bằng tay.

EXE bộ cài chưa ký Authenticode. Chữ ký driver tải từ Microsoft và chữ ký EXE bộ cài là hai thành phần khác nhau. Bộ cài không tự tắt các thiết lập bảo mật Windows.
