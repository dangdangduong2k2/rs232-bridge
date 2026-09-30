# Tên sản phẩm trong Windows và Chrome

**Trạng thái: đã chuẩn bị biến đổi source; chưa build, ký hoặc cài driver mới.** EXE RC6 phát hành vẫn dùng driver Microsoft Catalog ban đầu. Chạy script dưới đây không thay driver đang hoạt động và không thay bảo mật Windows.

| Vị trí | Tên dự kiến |
|---|---|
| COM phía ứng dụng (A, hiện COM52) | Nation COM Port |
| COM phía worker (B, hiện COM53) | Nation Bridge Internal |
| Bus | Nation RS232 Bridge Bus |
| Dịch vụ driver | Nation RS232 Bridge Serial Driver |

Chrome ưu tiên `DEVPKEY_Device_BusReportedDeviceDesc`, trong khi bộ cài hiện chỉ đổi FriendlyName. Trên máy thử, FriendlyName của COM52 là Nation COM Port nhưng BusReportedDeviceDesc vẫn là com0com. Source com0com 3.0.0.0 trả chuỗi đó trực tiếp ở `sys/pnp.c`, hàm `PdoPortQueryDevText`.

## Chuẩn bị source

Tại thư mục repo:

```powershell
.\driver-branding\prepare.ps1
```

Mặc định tạo một thư mục mới dưới `test-results`; có thể truyền `-OutputDirectory C:\Build\NationBrandedDriver` với đường dẫn chưa tồn tại. Script kiểm tra SHA256 archive upstream, giải nén, chỉ sửa `sys/pnp.c` và ba INF, ghi manifest hash trước/sau. Giữ archive, copyright, license, tên file, service key và hardware ID upstream. Việc giữ định danh kỹ thuật không có nghĩa thiết bị sẽ không còn nhận diện được là dựa trên com0com.

Trong `sys/pnp.c`, dùng quan hệ A/B có sẵn của upstream để trả hai tên khác nhau. Quan hệ này khớp bộ cài: `NationPort=ports["A"]`, `BridgePort=ports["B"]`. Thay tên bus do driver báo lên mới giải quyết hộp chọn cổng Chrome; chỉ sửa INF/FriendlyName không đủ.

## Công việc còn lại trước khi phân phối

1. Build và kiểm tra driver bằng toolchain WDK tương thích. Source upstream dùng `sources`/`makefile` kiểu DDK cũ; không coi việc có Visual Studio hoặc Windows SDK là đã có WDK hoặc build thành công. Máy chuẩn bị source có MSVC/Windows SDK nhưng chưa tìm thấy WDK kernel headers/toolchain.
2. Hoàn thiện metadata bản phát hành và nhà phát hành thực tế; tạo catalog mới, thực hiện quy trình ký driver phát hành phù hợp Windows đích. Chữ ký của gói Microsoft Catalog cũ không áp dụng cho SYS/INF đã sửa. Source chuẩn bị chưa có catalog/chữ ký mới.
3. Kiểm thử trên máy thử riêng: driver load, COM hai chiều, peer-open DSR, reconnect, Start/Stop, tên hai cổng qua PnP và hộp chọn Chrome sau khi enumerate lại. Kiểm tra metadata thực tế trước khi tuyên bố đổi tên thành công.
4. Chỉ sau khi có gói đã ký và kiểm thử, cập nhật `SetupEngine.PrepareMicrosoftDriver` sang gói đó cùng SHA256 mới, thêm nâng cấp/khôi phục và phát hành EXE mới. Bộ cài hiện sẽ tải lại gói cũ nên không thể chỉ chép source đã sửa vào payload.

Giữ `THIRD_PARTY_NOTICES.md`, GPLv2 và source tương ứng khi đóng gói. Đây là tên của sản phẩm bridge độc lập; không đổi tác giả upstream hoặc tuyên bố driver chính hãng Nation. Không phát hành driver chưa ký như một bộ cài dùng ngay.

## Nguồn kỹ thuật

- [Chromium serial enumerator](https://chromium.googlesource.com/chromium/src/+/b45c852011e34cd037b193371e5c6399fa64b3cb/services/device/serial/serial_device_enumerator_win.cc): ưu tiên tên do bus báo lên.
- [Microsoft: BusReportedDeviceDesc](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-busreporteddevicedesc): chỉ đọc đối với installer; PnP lấy từ driver.
- [Microsoft: kernel-mode signing](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-): điều kiện chữ ký driver.
