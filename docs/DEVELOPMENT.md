# Build, test và phát hành

## Chuẩn bị

Windows x64 với Windows PowerShell 5.1 và .NET Framework 4.5+; compiler tại `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`. Không cần Visual Studio, NuGet hoặc .NET SDK mới để chạy các script hiện có. Các DLL và công cụ cần build có trong `payload/` và `bridge-tests/vendor/`; đọc [thông tin bên thứ ba](../THIRD_PARTY_NOTICES.md).

```powershell
git clone https://github.com/dangdangduong2k2/rs232-bridge.git
Set-Location rs232-bridge
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\test.ps1
```

## Kết quả build

- `payload/bin/x86` và `payload/bin/x64`: worker, config và DLL ZK đúng kiến trúc.
- `payload/NationComService.exe`: dịch vụ Windows.
- `payload/diagnostics`: helper và DLL Nation nguyên bản.
- `payload.zip`: tài nguyên nhúng.
- `NationComPortSetup.exe`: bộ cài tự giải nén hoàn chỉnh.
- `test-results`: executable test và báo cáo; không đưa vào Git.

Build không tự cập nhật EXE đã chốt trong `dist/`. EXE build lại có thể khác hash vì timestamp/đóng gói; checksum phát hành xác nhận file cụ thể, không khẳng định build tái lập từng byte.

## Kiểm thử tự động

`build.ps1` chạy kiểm tra cấu hình/đóng gói. `test.ps1` chạy parser, Write, tích hợp SDK Nation với backend mô phỏng tại `127.0.0.1:18162`, helper, dừng bằng stop-file và kiểm tra SHA256 từng file giải nén.

Chạy build trước test. Cổng TCP 18162 phải rảnh. Các test này không cài driver, không mở COM vật lý, không ghi lên thẻ thật. Workflow GitHub Actions `Windows build and tests` chạy trên Windows và lưu EXE cùng báo cáo test làm artifact.

Để chạy mô phỏng thủ công sau build:

```powershell
.\payload\bin\x64\NationZkBridge.exe --simulate --antennas 4 --listen 18160
```

Ứng dụng thử kết nối TCP `127.0.0.1:18160`. Dữ liệu và identity mô phỏng là giả; không dùng làm bằng chứng phần cứng. Nhấn Ctrl+C để dừng.

## Thử phần cứng

Làm theo [hướng dẫn cài](INSTALLATION.md), sau đó kiểm tra helper, đọc một lần/liên tục, Stop, chọn anten, lọc, công suất và ghi/đọc lại trên thẻ thử. Ghi nhận riêng module/firmware, Windows, từng anten, reboot/hot-unplug và thời lượng chạy. Không gộp kết quả mô phỏng thành kết quả phần cứng. Danh sách đã đạt và phần chưa nghiệm thu ở [VALIDATION.md](VALIDATION.md).

## Chuẩn bị bản phát hành

Kiểm thử Q/Session phần cứng (chỉ chạy khi reader rảnh, sau build/test):

```powershell
.\test-results\BasebandSdk.exe COM52:115200 COM49
```

Bài này dùng Nation SDK qua bridge, đóng kết nối rồi đọc độc lập CFG9 bằng ZK SDK. Có Set tạm Q/Session và khôi phục cặp ban đầu trong `finally`; không ghi dữ liệu thẻ. Cần copy DLL ZK x64 từ `payload/bin/x64` vào `test-results` trước khi chạy. File log/kết quả nằm ngoài Git để tránh công bố dữ liệu máy/thẻ.

1. Cập nhật phiên bản, tài liệu, lỗi đã biết và kiểm chứng.
2. Build và test thành công.
3. Sao chép EXE vừa build và tạo checksum:

```powershell
Copy-Item .\NationComPortSetup.exe .\dist\RS232-Bridge-Setup.exe -Force
$releaseHash = (Get-FileHash .\dist\RS232-Bridge-Setup.exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content .\dist\SHA256SUMS.txt "$releaseHash  RS232-Bridge-Setup.exe" -Encoding ASCII
```

4. Kiểm tra staged diff, tài liệu liên kết và không commit log/cấu hình máy/thẻ thật.
5. Commit, tạo tag cho phiên bản, push và tạo GitHub Release kèm EXE + `SHA256SUMS.txt`. Bản 0.5 dùng tag `v0.5.0-rc.2` và đánh dấu prerelease.
6. Tải lại asset từ release và đối chiếu SHA256 với file trong `dist/`. Không sửa đè asset của một bản đã công bố; thay đổi tiếp theo dùng phiên bản mới.

GitHub tự cung cấp source ZIP/TAR theo tag. Người dùng cuối chỉ cần EXE và tài liệu cài; người sửa code clone repository.
