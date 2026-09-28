# Cấu hình RF — 0.5 RC

Bridge dùng giao thức Nation và DLL ZK gốc, không thay `GReaderApi.dll`. Bảng dưới dựa trên **menu Nation GReaderTool v0.39.0.0 được cung cấp** và SDK Ex10 V6.8. PDF Nation cũ gán mode 4 khác menu hiện tại; không dùng bảng PDF đó để quyết định mode 4.

## EPC Speed

Các mục Nation là **preset tương thích**, không bảo đảm RF giống từng thông số. Set dùng `SetExtProfile`; Get luôn đọc profile ZK thật trước khi trả preset Nation. Không dùng kết quả legacy 0 để kết luận module đang ở profile 0. Nếu profile thật khác giá trị đã ghi nhận, bridge bỏ nhãn cũ và dịch lại hoặc báo chưa hỗ trợ.

| Nation | ZK profile | RF ZK thực tế | So với nhãn Nation |
|---|---:|---|---|
| 0 | 205 | FM0, 50 kHz, Tari 20 µs | Nation 40 kHz, 25 µs |
| 1 | 146 | Miller4, 250 kHz, Tari 20 µs | Nation 25 µs |
| 2 | 141 | Miller4, 320 kHz, Tari 20 µs | Nation 300 kHz, 25 µs |
| 3 | 203 | FM0, 426 kHz, Tari 12.5 µs | Nation 400 kHz, 6.25 µs |
| 4 | 123 | Miller2, 320 kHz, Tari 20 µs | Trùng ba thông số trên menu |
| 5 | 103 | FM0, 640 kHz, Tari 6.25 µs | Trùng ba thông số trên menu |
| 6 | 241 | Miller4, 320 kHz, Tari 20 µs | Trùng ba thông số trên menu |
| 7 | 241 | Miller4, 320 kHz, Tari 20 µs | Nation 400 kHz, 6.25 µs; dùng preset M4 320 |
| 10 / 11 | 146 | Miller4, 250 kHz, Tari 20 µs | Nation Tari 12.5 / 6.25 µs |
| 12 / 13 | 141 | Miller4, 320 kHz, Tari 20 µs | Nation 300 kHz, Tari 12.5 / 6.25 µs |
| Auto (255) | 146 | Preset cân bằng cố định | Quy ước bridge, không phải tự điều chỉnh RF theo thẻ |

Các preset cùng profile ZK có thể cho cùng hiệu năng. Bridge nhớ lựa chọn để Get hiển thị đúng mục vừa Set, nhưng chỉ dùng lựa chọn đó khi Get ZK còn khớp. Thông số này không phải cam kết số thẻ/giây. Chưa hỗ trợ tùy chỉnh rời Tari/DR/Miller/RTcal/TRcal từ Nation. Firmware không nhận profile sẽ trả lỗi, không có Set giả thành công.

## Tần số

Không quy đổi gần đúng tần số. Các kênh hỗ trợ dưới đây khớp MHz thực tế.

| Band Nation | Band ZK | Kênh ZK cho toàn dải bridge | MHz |
|---:|---:|---|---|
| 0 | 1 | 2–17 | 920.625–924.375, bước 0.25 |
| 1 | 8 | 2–17 | 840.625–844.375, bước 0.25 |
| 3 | 2 | 0–49 | 902.75–927.25, bước 0.5 |
| 4 | 9 | 0–3 | 865.7, 866.3, 866.9, 867.5 |
| 5 | 31 | 0–3 | 916.8–920.4, bước 1.2 |
| 13 | 9 | 1–3 | 866.3, 866.9, 867.5 |
| 15 | 1 | 0–19 | 920.125–924.875, bước 0.25 |

Band 4: nếu Nation hiển thị thêm kênh 868.1 MHz thì kênh đó không nằm trong bảng ZK band 9 và bị từ chối. Auto ở band 4 dùng bốn kênh trong bảng trên. Band 2 của Nation là hai dải rời 840/920 MHz, không phải band 2 của ZK; không hỗ trợ gộp hai dải bằng một lệnh ZK.

F-hop hỗ trợ một kênh hoặc danh sách kênh liên tiếp. Danh sách rời rạc, trùng kênh, ngoài dải bị từ chối trước khi ghi; không âm thầm biến `{0,2}` thành `{0,1,2}`. Auto chọn toàn dải đã ánh xạ; không hỗ trợ chế độ dò kênh tự động thứ ba của Nation. Danh sách capabilities chỉ công bố band có ánh xạ. Firmware vẫn có thể từ chối tùy model/khu vực. Bảng firmware không thay thế quy định sử dụng tần số tại nơi triển khai.

## Công suất và lưu cấu hình

- Get trả vector thật từng anten bằng `GetAntennaPower`. Số phần tử phải khớp cấu hình 1 hoặc 4 anten.
- Set một phần đọc các giá trị hiện tại, thay anten được chọn và gửi vector bằng `SetAntennaPower`; đọc lại độc lập, sai kết quả trả lỗi.
- Công suất, band và profile dùng cờ lưu/tạm của ZK. Việc đọc lại đúng ngay sau Set không chứng minh đã lưu qua mất nguồn.
- Q, Session, SearchType và bộ lọc tag là logic bridge. Chúng giữ qua đóng/mở Nation. Khi lưu, chúng nằm trong `%ProgramData%\NationComPort\radio-state.xml`; ghi file tạm rồi thay thế atomic. Chỉ trường được yêu cầu lưu được cập nhật, không lưu lẫn các thay đổi tạm khác.
- Trạng thái tạm của bridge mất khi worker/dịch vụ khởi động lại. Trạng thái tạm của module phụ thuộc mất nguồn/reset module. Nhãn profile/kênh lưu ở bridge chỉ được dùng khi khớp Get phần cứng.
- Dừng inventory trước khi Set cấu hình RF. Chế độ cấu hình không tự phát lệnh đọc/ghi thẻ.

RC2: bỏ band Nation 12 khỏi capabilities và Set vì module type 0x75 firmware 2.8 trả ZK 0xFF cho bảng kênh tương ứng. Lỗi từ chối tham số/profile được dịch thành không hỗ trợ thay vì báo nhầm lỗi lưu.
