# driver RS232 brigde

Bộ chuyển giao thức để phần mềm Nation dùng `GReaderApi.dll` kết nối module ZK qua RS232, giữ nguyên phần mềm Nation.

**Bản thử nghiệm 0.4 RC.** Đã thử đọc và ghi EPC/User trên module ZK 4 anten; chưa thay thế toàn bộ SDK Nation. Xem [các lỗi đã biết](docs/KNOWN_ISSUES.md).

## Tải và cài

**[Tải EXE](https://github.com/dangdangduong2k2/rs232-bridge/releases/download/v0.4.0-rc.1/RS232-Bridge-Setup.exe)** · [Trang phát hành](https://github.com/dangdangduong2k2/rs232-bridge/releases/tag/v0.4.0-rc.1)

1. Cắm module ZK, đóng phần mềm đang dùng cổng COM.
2. Chạy `RS232-Bridge-Setup.exe`, bấm **setup**, xác nhận quyền quản trị Windows.
3. Máy mới chọn COM vật lý, baud và 1/4 anten một lần. Máy đã cài dùng cấu hình lưu sẵn.
4. Sau thông báo thành công, mở Nation, chọn **RS232 → Nation COM Port (COMx) → 115200**. Nếu Nation chỉ hiện số COM, chọn số bộ cài thông báo.

Không chọn `Nation Bridge Internal` hoặc COM vật lý ZK trong Nation. Dịch vụ tự chạy cùng Windows. **cancel** đóng bộ cài.

Windows x64, .NET Framework 4.5 trở lên; cần Internet khi tải driver lần đầu. Driver USB của adapter cần có sẵn. EXE bộ cài chưa ký Authenticode; driver COM ảo tải từ Microsoft Update Catalog và kiểm tra SHA256.

## Chức năng chính

- Đọc EPC/TID/User/Reserved, chọn anten, lọc EPC/TID, chống trùng, RSSI, Start/Stop.
- Ghi EPC/User và chuyển lệnh ghi 6C theo vùng nhớ, bộ lọc và mật khẩu; mỗi lệnh 1–64 word. Khả năng ghi phụ thuộc bộ nhớ và trạng thái khóa của thẻ.
- Cài công suất chung cho các anten; chưa hỗ trợ công suất riêng từng anten.

Chưa hỗ trợ đầy đủ các chức năng Nation như SuperRW, Lock/Kill, GPIO, BlockWrite, firmware và các giao thức thẻ khác. Chi tiết kiểm chứng trong [VALIDATION.md](docs/VALIDATION.md).

## Build và kiểm thử

Trên Windows, tại thư mục repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\test.ps1
```

Build tạo `NationComPortSetup.exe`. Test dùng dữ liệu mô phỏng, không cài driver và không mở COM thật. EXE phát hành nằm trong `dist/`, SHA256 ở `dist/SHA256SUMS.txt`.

| Thư mục | Nội dung |
|---|---|
| `src/` | Bộ cài Windows, dịch vụ và kiểm tra kết nối |
| `bridge-src/` | Giao thức Nation, backend ZK, xử lý đọc/ghi |
| `bridge-tests/` | Kiểm thử giao thức và DLL Nation gốc |
| `payload/` | Thư viện SDK và công cụ COM cần cho build |
| `dist/` | EXE để gửi người dùng |

Nhật ký cài: `%ProgramData%\NationComPort\setup.log`. Nhật ký dịch vụ: `%ProgramData%\NationComPort\logs\service.log`.

Thông tin thư viện và giấy phép: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Tài liệu đầy đủ

| Tài liệu | Nội dung |
|---|---|
| [INSTALLATION.md](docs/INSTALLATION.md) | Yêu cầu, cài mới, nâng cấp, đổi thiết bị, gỡ cài đặt |
| [USER_GUIDE.md](docs/USER_GUIDE.md) | Chọn COM, đọc/ghi thẻ, công suất, vận hành hằng ngày |
| [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | COM bị chiếm, driver, mất kết nối, lỗi ghi và lấy log |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Luồng dữ liệu, thành phần source, giao thức và cấu hình |
| [DEVELOPMENT.md](docs/DEVELOPMENT.md) | Build, test, chạy mô phỏng, đóng gói và phát hành |
| [VALIDATION.md](docs/VALIDATION.md) | Phạm vi đã kiểm chứng trên mô phỏng và phần cứng |
| [KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md) | Lỗi còn mở và giới hạn tương thích |
| [CHANGELOG.md](CHANGELOG.md) | Thông tin bản phát hành |

Khi gửi cho người dùng, gửi link EXE cùng hướng dẫn cài và danh sách lỗi đã biết. Không cần gửi hai bộ SDK gốc hoặc thay DLL trong phần mềm Nation.
