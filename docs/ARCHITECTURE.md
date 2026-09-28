# Kiến trúc và phạm vi giao thức

## Luồng dữ liệu

```text
Phần mềm Nation + GReaderApi.dll gốc
  ↕ giao thức Nation trên RS232
Nation COM Port (đầu A)
  ↕ cặp cổng com0com
Nation Bridge Internal (đầu B)
  ↕ NationZkBridge.exe
Giải frame Nation → handler → IReader / ITagWriter → UHFReader288.dll
  ↕ COM vật lý, giao thức module ZK
Module ZK → kết quả → frame/callback Nation → phần mềm Nation
```

Đây là bộ chuyển giao thức ngoài ứng dụng. Không sửa EXE Nation và không thay thế API assembly `GReaderApi.dll`. Driver kernel com0com chỉ cung cấp cặp COM; logic chuyển đổi chạy trong tiến trình .NET do dịch vụ Windows quản lý. DLL ZK native được chọn đúng x86/x64 của worker.

## Thành phần

| File | Trách nhiệm |
|---|---|
| `src/Setup.cs` | UI, giải nén payload, tải/kiểm tra CAB, tạo cặp COM, đăng ký/gỡ dịch vụ |
| `src/Common.cs` | Cấu hình XML, tìm thiết bị, kiểm tra COM và phản hồi Nation |
| `src/Service.cs` | Chạy worker, theo dõi thiết bị, dừng/thử lại và ghi log |
| `src/NationSerialCheck.cs` | Kiểm tra thông tin qua DLL Nation nguyên bản |
| `bridge-src/Program.cs` | Frontend COM hoặc TCP loopback, quản lý phiên, dừng worker |
| `bridge-src/Protocol.cs` | Đóng/gỡ frame, CRC, buffer dữ liệu chia nhỏ hoặc gộp |
| `bridge-src/Bridge.cs` | Dispatch lệnh, phản hồi, inventory worker, thiết lập phiên |
| `bridge-src/Models.cs` | Kiểu dữ liệu, giao diện backend và mô phỏng |
| `bridge-src/TagWrite.cs` | Kiểm tra/giải yêu cầu ghi |
| `bridge-src/ZkReader.cs` | Gọi SDK native ZK, chuyển dữ liệu và lỗi |

Worker theo dõi DSR khi dịch vụ bật `--watch-peer yes`: mở backend khi ứng dụng mở đầu COM Nation; kết thúc phiên sẽ hủy inventory và giải phóng backend. Không xóa buffer sau khi đóng backend vì request đầu tiên của phiên mới có thể đã nằm trong buffer.

## Các khóa lệnh có handler

Đây là key dùng trong `Bridge.Handle`, không phải toàn bộ trường control trên đường truyền.

| Key | Chức năng | Giới hạn chính |
|---|---|---|
| `0x100` | Reader info | Thông tin bridge/backend, không giả danh nguyên reader Nation |
| `0x101` | Phiên bản | Chuyển thông tin phiên bản backend |
| `0x103` | Baud frontend | Trả baud phía Nation |
| `0x112` | Heartbeat | Echo payload 4 byte |
| `0x200` | Capabilities | Phạm vi backend công bố |
| `0x201` / `0x202` | Set/Get power | Vector thật; Set một phần dùng read/modify/write |
| `0x203` / `0x204` | Set/Get region | Bảng ánh xạ theo MHz, xem RADIO_MAPPING.md |
| `0x205` / `0x206` | Set/Get frequency | Một kênh hoặc dải liên tiếp, không tự mở rộng danh sách rời rạc |
| `0x20B` / `0x20C` | Set/Get baseband | Preset Ex10, Q/session/target và persistence |
| `0x209` / `0x20A` | Set/Get tag reporting | Chống trùng/RSSI giữ qua reconnect và có lưu file |
| `0x210` | Inventory/read 6C | Chọn anten, lọc và vùng nhớ trong phạm vi parser |
| `0x211` | Write 6C | 1–64 word; EPC/User đã thử thật |
| `0x2FF` | Stop | Dừng inventory |

Lệnh ngoài bảng trả lỗi chưa hỗ trợ. RS485 flags chưa hỗ trợ. Không dùng bảng này để suy ra mọi tùy chọn của một message Nation đều được chuyển đổi.

## Cấu hình

`settings.xml` do bộ cài tạo, gồm `PhysicalPort`, `PhysicalId`, `NationPort`, `BridgePort`, `Baud`, `Antennas`, `Pair`. Ba COM phải khác nhau; số anten là 1 hoặc 4. Dịch vụ dùng `PhysicalId` để tìm lại cổng vật lý, không chỉ dựa vào số COM.

Dịch vụ tên `NationZkComBridge`, log ở `%ProgramData%\NationComPort\logs\service.log`; xoay log khi file vượt 2 MiB, giữ một bản `.1`. Frontend TCP phát triển chỉ lắng nghe `127.0.0.1`; bộ cài thông thường dùng COM.

`bridge-src/Radio.cs` chứa bảng kênh và lưu trạng thái atomic. `ProfileMap.cs` chứa preset tương thích với menu Nation v0.39. `--state-file` do dịch vụ truyền vào `%ProgramData%\NationComPort\radio-state.xml`.
