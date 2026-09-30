# Tốc độ inventory — RC6

## RC6: sửa khoảng ngừng định kỳ

RC5 đã dùng Scenario nhưng vẫn dừng để bổ sung PC theo từng nhóm tối đa 32 khóa. Sau `StopRead`, bridge gọi `GetRfidTagData` thêm một lần với mục đích lấy dữ liệu còn lại. Đo trực tiếp cho thấy đây là receive có timeout: riêng lần gọi sau Stop chờ 1291–1304 ms. Đó là phần mềm tự tạo khoảng ngừng, không phải Nation yêu cầu Stop.

RC6 bỏ lần nhận sau Stop. Mọi frame hoàn chỉnh đã nhận trước Stop được xử lý; parser bắt đầu mới sau restart, không ghép frame dở dang qua hai luồng. Mỗi đợt bổ sung PC chọn tối đa 4 khóa EPC/anten và không bắt đầu lệnh tiếp theo nếu đã dùng 200 ms; một lệnh native đang chạy không thể hủy giữa chừng. Thẻ lỗi PC có thời gian thử lại 30, 60 rồi tối đa 120 giây. Các đợt bổ sung cách nhau ít nhất 5 giây; nếu không có khóa đến hạn thì không Stop/Start.

Điều này giảm khoảng ngừng do bridge, **không cam kết luồng RF hoàn toàn không có khoảng nghỉ**. Thẻ mới cần PC vẫn có thể tạo khoảng dừng ngắn; thẻ PC lỗi có thể xuất hiện muộn hoặc chưa tới Nation. Chuyển Target/anten, điều kiện RF và timeout thiết bị vẫn ảnh hưởng. Không phát lại EPC cũ để giữ count chạy trong khoảng không đọc được.

Log `SCENARIO pause` ghi lý do, số lần đọc PC, thời gian Stop và tổng thời gian; không ghi mã thẻ. Bài `InventoryHardwareSdk` đo khoảng cách callback (`REPORT GAPS`), gồm warmup nhưng không gồm chờ báo cáo đầu tiên và thời gian Stop.

### Đo khoảng ngừng ngày 30/09/2026

Cùng module type 0x75/firmware 2.8, COM49/115200, chọn ANT1–4, Q2/S0, không đổi công suất/profile/tần số:

| Bản / đường nhận | Lượt / thời gian | Lượt/giây | Khoảng callback lớn nhất | Khoảng ≥ 1 giây |
|---|---:|---:|---:|---:|
| RC5 đã cài / COM52 | 4848 / 40.319 s | 120.2 | 3543 ms | 5 |
| RC6 / TCP loopback | 7004 / 45.260 s | 154.8 | 420 ms | 0 |
| RC6 đã cài / COM52 | 9332 / 60.360 s | 154.6 | 421 ms | 0 |

Lượt RC6/TCP cũng không có khoảng ≥ 500 ms. Log bảo trì PC cuối phép đo khoảng 342–385 ms; không còn 1.3 giây chờ receive sau Stop. SDK nhận trường EPC/PC/anten hợp lệ và Stop bình thường, không báo cáo muộn. RC6 còn 356 lượt chờ PC khi dừng, overflow=0; chưa thể coi mọi lượt EPC đã tới Nation. Đây là các phép đo tuần tự, khác đường nhận và tập thẻ nhận được (45/41 EPC), không quy toàn bộ chênh lệch tốc độ cho một thay đổi hoặc coi là bảo đảm thời gian thực.

Sau khi cài RC6, hash worker khớp build và dịch vụ Running. Lượt 60 giây qua COM52 nhận 9332 báo cáo, 41 EPC, không sai trường dữ liệu hoặc báo cáo sau Stop; cũng không có khoảng ≥ 500 ms. Log `returned=9332 forwarded=9332`, không lọc RSSI/chống trùng; còn 576 lượt chờ PC và overflow=0 khi Stop. Các khoảng dừng bảo trì ngắn vẫn tồn tại; kết quả này xác nhận đã bỏ các khoảng ngừng dài lặp lại trên bộ thẻ thử, không phải đã chuyển đầy đủ mọi frame EPC bất kể PC.

## RC5: cơ chế Scenario và kết quả trước sửa khoảng ngừng

Khi Nation chọn Inventory liên tục, không yêu cầu TID/User/Reserved và không có mask thẻ, bridge dùng `StartRead` / `GetRfidTagData` / `StopRead`, cùng cơ chế Scenario-mode của demo ZK. Không đổi phần mềm Nation hoặc cổng kết nối.

Đầu mỗi phiên, bridge đọc EPC/PC thật để tạo cache theo EPC + anten. EPC và RSSI tiếp theo luôn đến từ các frame Scenario mới, còn PC dùng giá trị thật đã đọc trong phiên. Thẻ mới chưa có PC được chờ trong hàng đợi giới hạn 4096 lượt; bridge tạm dừng Scenario để đọc PC của tối đa 32 khóa chưa biết mỗi lần rồi tiếp tục. Không đọc được PC thì chưa thể gửi báo cáo Nation cho lượt đó; quá giới hạn thì bỏ lượt cũ nhất và tăng bộ đếm `overflow`. Không nhân count hoặc suy PC từ độ dài EPC.

**Đây là lựa chọn ưu tiên EPC/count; PC không được bảo đảm mới ở từng lượt.** Cache chỉ tồn tại trong một Start–Stop và bị bỏ trước phiên tiếp theo. Trong lúc inventory, lệnh ghi hoặc cấu hình phần cứng trả busy; Stop trước khi ghi. Thay đổi PC bởi reader khác trong lúc cùng phiên sẽ chưa phản ánh ngay. Các thẻ trùng EPC trên cùng anten dùng chung PC cache.

Đọc một lần, có mask hoặc yêu cầu đọc thêm bộ nhớ vẫn dùng đường RC4. Muốn bắt buộc PC mới ở từng lượt EPC-only, kỹ thuật viên chọn `--epc-mode fresh-pc` khi chạy worker, hoặc `<EpcMode>fresh-pc</EpcMode>` trong `settings.xml` và khởi động lại dịch vụ. Mặc định RC5 là `scenario`; file cấu hình cũ chưa có trường này cũng nhận mặc định đó.

Scenario tạm dùng anten theo yêu cầu Nation, tắt TID/mask gốc khi cần và khôi phục khi kết thúc hoặc lỗi. Không lưu flash, không đổi công suất/tần số/profile hoặc dwell/interval CFG7. Vì vậy thời gian chuyển anten theo cấu hình Scenario của module; ở máy thử CFG7 dwell đang là 14 giây. Chọn Target A/B được giữ; Nation Auto A/B với Session>0 luân phiên mục tiêu theo cửa sổ 1 giây.

### Đối chiếu 30/09/2026

Trên cùng module type 0x75/firmware 2.8, COM49/115200, ANT2, Q=2/S0:

| Đường đọc | Số lượt | Thời gian | Lượt/giây | EPC khác nhau |
|---|---:|---:|---:|---:|
| Scenario SDK ZK trực tiếp | 1572 | 10.030 s | 156.7 | 9 |
| RC5 qua SDK Nation/TCP | 1512 | 10.357 s | 146.0 | 9 |
| RC5 đã cài, SDK Nation/COM52, ANT2 | 1034 | 10.785 s | 95.9 | 8 |
| RC5 đã cài, SDK Nation/COM52, chọn ANT1–4 | 1777 | 16.262 s | 109.3 | 43 |

Phép đo Nation bao gồm khởi tạo PC và Start/Stop. SDK nhận đủ 1512 báo cáo bridge phát, không sai EPC/PC/antenna, kết thúc do Stop bình thường. Cuối phiên `awaiting_pc=0`, `overflow=0`. Đây là lượt đo tuần tự, không phải cam kết giữ nguyên tỷ lệ trên mọi tập thẻ.

Hai lượt COM52 là kiểm tra sau khi cài RC5, dùng dịch vụ và DLL Nation gốc; không sai trường dữ liệu, Stop bình thường và không có báo cáo muộn. Lượt chọn bốn anten có `returned=1777 forwarded=1777`, không lọc RSSI/chống trùng, nhưng vẫn còn **33 lượt đang chờ PC** khi dừng (`overflow=0`). Số forwarded chỉ tính các bản ghi đã có PC, không có nghĩa mọi frame EPC gốc đều đã tới Nation. Bản đã cài chưa đạt tốc độ Scenario trực tiếp trong các lượt này; không dùng số đo TCP 146.0 làm tốc độ COM52. Các lượt khác nhau về thời điểm và tập EPC nhận được, nên chưa xác định phần chênh lệch do COM, truy vấn PC hay điều kiện RF.

Unit test kiểm tra frame chia nhỏ/gộp, CRC, anten, phase, cache/queue có giới hạn, lượt trùng, cache mới theo phiên, busy và Stop. Test phần cứng chủ động đã đặt tạm CFG10/CFG11, làm callback lỗi có chủ đích và xác nhận anten/TID/mask được khôi phục; cuối bài các giá trị ban đầu cũng được đọc lại xác nhận.

Log `SCENARIO` bổ sung `raw` (frame Scenario), `emitted_including_warmup` (báo cáo EPC thật từ warmup và Scenario), `awaiting_pc`, `overflow`. Không so trực tiếp raw và emitted vì warmup cũng đọc EPC thật. Stop có thể hủy các bản ghi chưa gửi/đang chờ PC; không gửi sau Stop.

## Lịch sử RC4

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
