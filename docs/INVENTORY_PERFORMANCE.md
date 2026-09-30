# Tốc độ inventory — RC4

## Nguyên nhân đã xác nhận

RC3 gọi `Inventory_G2` lấy EPC, sau đó `ReadData_G2` lấy PC từng lượt thẻ. Chỉ sau khi đọc PC thành công mới gửi báo cáo Nation. Cách này vừa thêm lượt truyền serial cho mỗi EPC, vừa bỏ báo cáo khi lần đọc PC sau inventory trả lỗi. Trong thử nghiệm 30/09/2026, nhiều lệnh PC trả `0xFB`.

RC4 dùng `InventoryMix_G2` đọc EPC kèm một word PC (bank 1, word 1) ngay trong cùng lượt inventory. Parser ghép đúng hai packet liên tiếp theo sequence 7 bit, kể cả vòng 127→0; không suy PC từ chiều dài EPC. Mỗi lần EPC xuất hiện vẫn là một báo cáo, không gộp count theo EPC duy nhất.

Nếu EPC thiếu PC đi kèm, bridge mới đọc PC riêng cho lượt đó. Nếu vẫn lỗi, không gửi PC giả. Firmware từ chối mixed inventory bằng `FD/FE/EE` sẽ dùng đường cũ trong kết nối hiện tại. Không fallback sau timeout hoặc lỗi truyền thông. TID/User/Reserved vẫn cần các lệnh đọc bổ sung khi ứng dụng yêu cầu.

## Kết quả đo trên máy thử

Module type `0x75`, firmware 2.8, COM49/115200, ANT1, Q=2, Session=0, chỉ EPC, không thay công suất/profile/tần số hoặc dữ liệu thẻ. Thử xen kẽ hai lượt RC3 và hai lượt backend mới, mỗi lượt tối thiểu 3 giây:

| Backend | Báo cáo hợp lệ | Thời gian | Lượt/giây | EPC khác nhau |
|---|---:|---:|---:|---:|
| RC3, lượt 1 | 9 | 3.681 s | 2.4 | 5 |
| RC4, lượt 1 | 168 | 3.156 s | 53.2 | 35 |
| RC3, lượt 2 | 10 | 3.536 s | 2.8 | 5 |
| RC4, lượt 2 | 170 | 3.187 s | 53.3 | 38 |

Qua DLL Nation gốc → TCP loopback → bridge mới → COM49: 154 báo cáo, 35 EPC khác nhau, 4.436 giây bao gồm Start/Stop (~34.7 lượt/giây). Không lỗi trường EPC/PC/antenna; không còn báo cáo sau Stop. Backend trực tiếp và phép đo SDK có lịch đọc/Target khác nhau, không lấy tỷ lệ hai phép đo này làm hao hụt truyền tải.

Sau khi cài RC4, qua DLL Nation gốc → COM52 → dịch vụ → COM49: 170 báo cáo, 36 EPC khác nhau, 4.396 giây (~38.7 lượt/giây, gồm Start/Stop). Log bridge ghi `returned=170 forwarded=170 rssi_filtered=0 duplicate_filtered=0`; SDK nhận đủ 170, không sai trường dữ liệu và không có báo cáo sau Stop. Hash worker đã cài khớp bản build. Những EPC không lấy được PC vẫn chưa được tính vào `returned`.

Đây là kết quả tại bộ thẻ/antenna đang thử, không phải cam kết tốc độ cho mọi môi trường. Count ZK EPC-only và Nation có PC/TID/User không đo cùng lượng công việc. Chưa kết luận đạt tốc độ của phần mềm ZK ở mọi chế độ.

## Đọc log và so sánh đúng

- `INVENTORY start`: anten, Q, Session, Target, số word đọc thêm, chống trùng và RSSI threshold.
- `INVENTORY PC`: số EPC, số lần phải đọc PC riêng, số thất bại, số bản ghi trả về. Không chứa mã thẻ.
- `INVENTORY counts`: tổng bản ghi backend trả về, số đã gửi Nation, số bị lọc RSSI/chống trùng, thời gian. Xuất mỗi khoảng 5 giây và khi dừng.
- Khi bấm Stop giữa một batch, `returned` có thể lớn hơn `forwarded`: các bản ghi chưa gửi bị hủy theo yêu cầu Stop.

So sánh bằng cùng số anten, công suất, profile, Q/Session/Target, khoảng đo và bộ thẻ. Nếu cần đếm mọi lần đọc, đặt chống trùng=0; tắt đọc thêm TID/User/Reserved khi chỉ cần EPC. Không đổi cấu hình của người dùng một cách tự động để làm số đo đẹp hơn.

Test tự động `MixedInventoryTests` kiểm tra PC thật, sequence wrap, packet thiếu/lệch, dữ liệu phase, lặp EPC và 100/100 báo cáo sang Nation khi chống trùng tắt. `InventoryHardwareSdk` là kiểm tra RF chủ động riêng, không chạy trong `test.ps1`.
