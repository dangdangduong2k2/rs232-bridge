# Cổng nội bộ từ RC7

Người vận hành chỉ chọn đầu công khai **Nation COM Port** (trên máy thử là COM52). Đầu nội bộ vẫn có tên COM53 trong cấu hình để dịch vụ mở trực tiếp, nhưng không xuất hiện trong danh sách serial thông thường. COM vật lý của reader vẫn xuất hiện riêng.

## Cơ chế

Installer chuyển riêng đầu B của cặp do nó quản lý sang `PortName=COM53,HiddenMode=yes` (số thực tế lấy từ cấu hình). Tên tường minh thay cho `COM#` đưa B sang lớp riêng CNCPorts. HiddenMode ngừng công bố PortName/device map/WMI nhưng giữ symbolic link để bridge mở trực tiếp. Chỉ bật HiddenMode mà giữ lớp Ports với `COM#` không đủ để ẩn khỏi các bộ liệt kê.

Đầu A giữ lớp Ports và số COM công khai. Các thông số truyền và `dsr=ropen` của B được giữ; bộ cài thử dữ liệu hai chiều và DSR trước khi chạy dịch vụ. Số COM nội bộ vẫn được driver đặt chỗ, không dùng nó cho adapter khác.

Chromium đọc `PortName` từ registry thiết bị và bỏ qua thiết bị nếu không đọc được trường này; xem [mã nguồn SerialDeviceEnumeratorWin](https://raw.githubusercontent.com/chromium/chromium/main/services/device/serial/serial_device_enumerator_win.cc). RC7 đã kiểm tra registry và danh sách serial Windows, chưa kiểm tra trực quan chooser Chrome sau cài. Nếu còn thấy danh sách cũ, đóng/mở lại hộp chọn hoặc khởi động lại Chrome.

## Phạm vi

- Device Manager/công cụ quản trị vẫn có thể thấy `Nation Bridge Internal` trong lớp CNCPorts.
- Đây không phải khóa truy cập bảo mật: chương trình biết chính xác tên có thể thử mở trực tiếp khi dịch vụ không giữ cổng. Khi chạy, dịch vụ giữ đầu B độc quyền.
- Driver Catalog nguyên bản vẫn dùng tên com0com ở một số giao diện. Ẩn đầu nội bộ và đổi tên đầu công khai là hai thay đổi riêng.

## Repair và khôi phục

Disconnect Nation rồi chạy bộ cài RC7 hoặc mới hơn để repair. Nó đọc cả cách khai báo COM cũ và đầu ẩn mới, kiểm tra đúng cặp, giữ số COM và áp dụng lại chế độ ẩn. Không chạy bộ cài RC6 trở về trước lên cấu hình này: bước tìm đầu B trong danh sách công khai của bản cũ có thể thất bại.

Nếu cài lỗi, giữ `settings.xml`, xem `%ProgramData%\NationComPort\setup.log` rồi repair bằng RC7. Không xóa cổng hoặc sửa registry thủ công. Bộ gỡ RC7 xác định cặp bằng cấu hình driver nên vẫn nhận ra đầu ẩn. Việc downgrade hoặc gỡ/cài lại cần kỹ thuật viên kiểm tra số COM; đợt này chưa nghiệm thu downgrade hoặc gỡ cặp thật đang sử dụng.
