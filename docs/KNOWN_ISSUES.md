# Trạng thái lỗi — 0.5 RC

## RC7: ẩn cổng nội bộ

- Đã xác nhận đầu B vắng trong `.NET SerialPort.GetPortNames()` và không công bố registry `PortName`; bridge vẫn đọc qua cặp COM được. Chưa kiểm tra trực quan hộp chọn cổng Chrome sau nâng cấp; Chrome có thể cần mở lại để bỏ danh sách cũ.
- Đây là tránh chọn nhầm trong danh sách, không phải ranh giới bảo mật. Công cụ quản trị vẫn có thể thấy thiết bị CNCPorts; ứng dụng biết tên COM nội bộ có thể thử mở khi dịch vụ không giữ nó.
- Tên public do driver báo cho Chrome vẫn có thể là com0com. RC7 giữ driver đã ký nguyên bản. [Chi tiết](INTERNAL_PORT.md).

## RC6: khoảng ngừng định kỳ

- Đã bỏ receive sau Stop gây timeout khoảng 1.3 giây trong RC5; hạn chế từng đợt đọc PC và giãn retry thẻ lỗi. Xem [cơ chế và phép đo](INVENTORY_PERFORMANCE.md).
- Thẻ mới thiếu PC vẫn cần dừng ngắn để bổ sung. Đây không phải bảo đảm RF không bao giờ gián đoạn; một lệnh native đang chạy có thể vượt ngân sách 200 ms. Thẻ đọc PC lỗi có thể chờ đến 120 giây trước lần thử tiếp theo; queue vẫn giới hạn 4096 lượt.

## RC5: EPC Scenario và PC cache

- EPC-only liên tục mặc định dùng Scenario. PC đọc thật rồi dùng lại trong phiên, không bảo đảm mới từng lượt; cache bỏ khi Stop. Thẻ trùng EPC trên cùng anten dùng chung PC cache. Có thể chọn `EpcMode=fresh-pc` nếu cần PC mới từng lượt.
- Thẻ mới chưa có PC phải chờ truy vấn bổ sung; PC đọc lỗi vẫn có thể làm thiếu lượt. Queue giới hạn 4096 lượt, log `overflow` nếu đầy. Không báo PC giả.
- Khi Scenario đang chạy, Get/Set phần cứng trả busy để không trộn lệnh với luồng dữ liệu; Stop trước khi cấu hình.
- Đã đo tốc độ trên module 4 anten, chọn ANT2 và ANT1–4/Q2/S0; chưa chứng nhận hiệu năng mọi Session/Target, module 1 anten hoặc mọi firmware. Qua COM52 đã cài đạt 95.9 và 109.3 lượt/giây, vẫn chậm hơn lượt đo Scenario trực tiếp 156.7 lượt/giây. Lượt chọn bốn anten còn 33 lượt chờ PC khi Stop. Không thay dwell/interval CFG7; module quyết định lịch chuyển anten.
- Xem [điều kiện đo](INVENTORY_PERFORMANCE.md).

## RC4: tốc độ inventory

- Đã xử lý điểm nghẽn đọc PC riêng sau mỗi EPC bằng mixed inventory. PC thiếu/lỗi vẫn cần đọc bổ sung; TID/User/Reserved vẫn tạo thêm lệnh, nên không bảo đảm count bằng phần mềm ZK chỉ đọc EPC.
- Xem [phép đo và điều kiện kiểm thử](INVENTORY_PERFORMANCE.md). Parser không tạo PC giả hoặc gộp nhiều lượt thành một báo cáo.

## RC3: Q/Session và Get đầu phiên

- RC2 chỉ lưu Q/Session ở bridge rồi truyền vào inventory. Kiểm thử reconnect RC2 chưa xác nhận cặp cấu hình CFG9 trên ZK; do đó không chứng minh đồng bộ với phần mềm ZK.
- RC3 đọc/ghi CFG9 thật, Get và inventory lấy lại dữ liệu trên module. Get đầu tiên được kiểm thử không Set trước và không cần file trạng thái.
- Các ô Q/Session trong Answer Mode của demo ZK là lựa chọn cục bộ của demo, không phải kết quả Get từ reader. `GetQS` cũ bị module thử trả 0xEE; dùng CFG9 để đối chiếu.

## Đã sửa trong source và bộ cài 0.5

- Thiếu Set/Get tần số: đã thêm handler band và working frequency, quy đổi kênh theo MHz.
- EPC speed bị chặn: đã nối extended profile ZK và kiểm tra Get sau Set. Một số mục dùng preset tương ứng, xem [RADIO_MAPPING.md](RADIO_MAPPING.md).
- Công suất chỉ đặt chung: đã dùng vector từng anten, cho phép yêu cầu một phần.
- Baseband/reporting từ chối `FF 00`: đã xử lý persistence; kiểm thử bằng DLL Nation gốc đạt.
- Baseband/reporting mất sau reconnect: state giữ qua phiên, chế độ lưu ghi file atomic. Kiểm thử restart đối tượng lưu trữ và reconnect TCP đạt.
- Setup COM bị chiếm: kiểm tra cổng Nation trước khi dừng dịch vụ/thay payload; yêu cầu đóng Nation nếu cổng còn bận.

## Đã nghiệm thu RC2 trên COM thật

- Nation SDK gốc → COM52 → dịch vụ bridge → ZK COM49, module type 0x75 firmware 2.8: 170 assertion đạt, gồm 13 EPC speed (có mode 5/FM0 640 kHz), 7 band, fixed/auto channel, power vector và sửa riêng một anten.
- Reconnect 100/350/700 ms đạt; Q/Session/reporting được giữ. Cấu hình ban đầu đã được khôi phục và đọc lại.
- Band Nation 12 bị firmware từ chối ZK 0xFF; đã bỏ khỏi capabilities/Set của RC2.
- Vẫn chưa nghiệm thu lưu RF qua mất nguồn, module 1 anten, tháo/cắm USB và chạy dài hạn.

## Giới hạn có chủ đích

- EPC speed là preset tương thích theo menu Nation v0.39, không bảo đảm Tari/BLF trùng ở mọi mục. Auto dùng preset cố định. Chỉ firmware có extended profile được hỗ trợ ở phần này.
- Không hỗ trợ band Nation hai dải rời, danh sách kênh không liên tiếp hoặc dò kênh tự động. Không giả lập thành công cho tùy chọn không có ánh xạ.
- SuperRW, Lock/Kill, GPIO, BlockWrite, firmware và toàn bộ các lệnh SDK ngoài bảng handler vẫn chưa hỗ trợ.
- RF anten 3–4 chưa nghiệm thu đọc thẻ trong đợt trước.

Xem [VALIDATION.md](VALIDATION.md) để phân biệt kiểm thử mới, kiểm thử cũ và phần chưa kiểm chứng.
