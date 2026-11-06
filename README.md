Nghiệp vụ tính phí gửi xe ô tô — mô tả bằng lời kèm biểu phí
Biểu phí:

Khung giờ		    Đơn giá			    Cách tính
06:00 – 17:29		15.000đ / 2 giờ		Tính theo lượt 2 giờ, lẻ làm tròn lên
18:00 – 23:59		20.000đ / 2 giờ		Tính theo lượt 2 giờ, lẻ làm tròn lên
00:00 – 05:59		100.000đ / lượt		Cố định, chạm là tính

- Bước 1: Xác định thời gian xe ở trong hầm.
Lấy giờ vào và giờ ra. Nếu xe ra ngày hôm sau so với ngày vào, nghĩa là xe đã qua đêm, ta phải chia thời gian thành hai phần: phần từ giờ vào đến hết 23 giờ 59 ngày đầu, và phần từ 0 giờ đến giờ ra ngày sau.
- Bước 2: Tính phí cho khung ban ngày (6 giờ sáng đến 17 giờ 29).
Đếm xem xe nằm trong khung này bao nhiêu phút. Lấy số phút đó chia cho 120, nếu không chia hết thì làm tròn lên 1. Số đó chính là số "lượt 2 giờ". Nhân với 15.000 đồng. Nếu xe chỉ ở trong khung này 1 phút thôi thì vẫn tính 1 lượt, tức 15.000 đồng.
- Bước 3: Tính phí cho khung buổi tối (18 giờ đến 23 giờ 59).
Cách tính y hệt bước 2, nhưng đơn giá là 20.000 đồng cho mỗi 2 giờ.
- Bước 4: Kiểm tra xem xe có qua đêm không.
Nếu xe có mặt ở trong hầm bất kỳ phút nào trong khoảng từ 0 giờ đến 5 giờ 59 phút — dù chỉ 1 phút — thì cộng thêm 100.000 đồng. Đây là phí cố định, không quan tâm xe ở bao lâu trong khung đó. Nói cách khác, chỉ cần xe "chạm" vào khung này là tính 100.000, không nhân theo giờ.
- Bước 5: Cộng lại.
Tổng tiền = phí ban ngày + phí buổi tối + phí qua đêm (nếu có).

Một số lưu ý:
Nếu xe vào lúc 5 giờ 30 sáng, thì phần từ 5 giờ 30 đến 5 giờ 59 rơi vào khung qua đêm, còn phần từ 6 giờ trở đi mới tính khung ban ngày.
Nếu dữ liệu sai, ví dụ giờ ra nhỏ hơn giờ vào mà không phải qua đêm, thì phải trả lỗi, không được tính.
Tóm lại, logic cốt lõi chỉ có ba câu: ban ngày tính theo 2 giờ một lượt, buổi tối tính theo 2 giờ một lượt, qua đêm tính cố định một lần. Cộng ba cái đó lại là ra tổng tiền.
