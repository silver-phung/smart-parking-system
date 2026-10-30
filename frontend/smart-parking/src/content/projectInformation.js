export const projectLinks = [
  {
    slug: "du-an",
    label: "Về dự án mô phỏng",
    summary: "Mục đích và phạm vi trải nghiệm",
  },
  {
    slug: "dieu-kien",
    label: "Điều kiện trải nghiệm",
    summary: "Những điều cần biết trước khi dùng",
  },
  {
    slug: "quyen-rieng-tu",
    label: "Quyền riêng tư & dữ liệu",
    summary: "Tài khoản, hình ảnh và phiên đăng nhập",
  },
  {
    slug: "gioi-han",
    label: "Giới hạn của bản demo",
    summary: "Dữ liệu bãi đỗ và chức năng thử nghiệm",
  },
  {
    slug: "chong-gia-mao",
    label: "Cảnh báo giả mạo",
    summary: "Không chuyển tiền hoặc chia sẻ mã OTP",
  },
  {
    slug: "lien-he",
    label: "Liên hệ & báo lỗi",
    summary: "Gửi phản hồi cho nhóm phát triển",
  },
];
export const projectInformation = {
  "du-an": {
    intro:
      "Đây là một dự án mô phỏng hệ thống quản lý bãi đỗ xe thông minh, phục vụ học tập, thực hành và trình diễn trong chương trình OJT.",
    sections: [
      {
        title: "Một môi trường để học và thử nghiệm",
        text: "Smart-Parking minh họa cách phối hợp giữa khách hàng, nhân viên cổng, nhân viên xác minh ô, quản lý và quản trị viên. Tên bãi, chỗ trống, hình minh họa và các phiên gửi xe trên website không xác nhận sự tồn tại hoặc khả năng phục vụ của một bãi đỗ ngoài đời.",
      },
      {
        title: "Không phải dịch vụ giữ xe thương mại",
        text: "Website này không cung cấp dịch vụ nhận giữ xe, không bán chỗ đỗ, không cam kết giữ chỗ thực tế và không phải kênh đặt xe hay thanh toán. Việc đăng ký tài khoản không tạo một hợp đồng giữ xe hoặc bảo đảm chỗ đỗ.",
      },
      {
        title: "Minh bạch ngay từ lần truy cập đầu tiên",
        text: "Những chức năng được giới thiệu là phạm vi trình diễn của dự án. Chức năng đang phát triển có thể chưa xuất hiện trên giao diện, tạm ngừng hoạt động hoặc thay đổi qua từng sprint. Bạn có thể xem các giới hạn và thông tin dữ liệu trước khi trải nghiệm.",
      },
    ],
  },
  "dieu-kien": {
    intro:
      "Hãy sử dụng website như một môi trường trải nghiệm phần mềm. Các hướng dẫn dưới đây giúp bạn thử nghiệm đúng phạm vi của dự án.",
    sections: [
      {
        title: "Trải nghiệm bằng dữ liệu phù hợp",
        text: "Ưu tiên dữ liệu mẫu, biển số giả lập và ảnh minh họa không chứa thông tin cá nhân. Nếu đăng ký qua OTP, hãy dùng email bạn có quyền truy cập và một mật khẩu riêng cho bản demo, không dùng lại mật khẩu quan trọng.",
      },
      {
        title: "Tôn trọng tài khoản và quyền truy cập",
        text: "Chỉ thao tác với tài khoản và dữ liệu bạn được phép sử dụng. Không dò mật khẩu, spam OTP, tải nội dung trái phép hoặc cố vượt quyền của các vai trò khác. Tài khoản nội bộ do Admin của nhóm cấp cho mục đích demo.",
      },
      {
        title: "Không sử dụng để quyết định việc gửi xe thực tế",
        text: "Không dựa vào số ô trống, trạng thái phiên hoặc khoản phí trong bản demo để di chuyển, giao xe hay chuyển tiền. Khi cần dịch vụ ngoài đời, hãy liên hệ trực tiếp đơn vị vận hành bãi đỗ mà bạn đã tự xác minh.",
      },
      {
        title: "Dữ liệu có thể thay đổi trong quá trình phát triển",
        text: "Nhóm có thể điều chỉnh tính năng hoặc đặt lại dữ liệu thử nghiệm khi kiểm thử và cập nhật sprint. Không dùng hệ thống này làm nơi lưu trữ duy nhất cho dữ liệu quan trọng.",
      },
    ],
  },
  "quyen-rieng-tu": {
    intro:
      "Mô phỏng nghiệp vụ không có nghĩa là mọi dữ liệu đều giả. Thông tin bạn chủ động nhập hoặc tải lên có thể được xử lý và lưu thật.",
    sections: [
      {
        title: "Thông tin có thể được lưu",
        text: "Tùy thao tác, hệ thống có thể lưu email, hồ sơ, phương tiện, biển số, ảnh, thời gian vào/ra và lịch sử thao tác. Email được dùng cho luồng xác minh OTP. Nhật ký kỹ thuật có thể chứa thời điểm truy cập, thông tin thiết bị hoặc địa chỉ IP.",
      },
      {
        title: "Các dịch vụ xử lý dữ liệu",
        text: "Backend hiện sử dụng cơ sở dữ liệu PostgreSQL trên Neon. Khi chức năng tương ứng được cấu hình, ảnh có thể được gửi đến Cloudinary để lưu trữ và dịch vụ nhận diện AI để phân tích; email xác minh được gửi qua dịch vụ email của dự án. Vì vậy, hãy tránh tải ảnh người thật, giấy tờ hoặc biển số thật không cần thiết.",
      },
      {
        title: "Phiên đăng nhập trong trình duyệt",
        text: "Website lưu thông tin phiên và token trong sessionStorage để giữ đăng nhập khi tải lại cùng tab. Bấm Đăng xuất khi dùng xong, nhất là trên máy dùng chung. Không gửi token, mật khẩu hoặc OTP qua kênh báo lỗi.",
      },
      {
        title: "Lưu giữ và yêu cầu xử lý dữ liệu",
        text: "Bản demo chưa công bố thời hạn xóa tự động; không nên mặc định dữ liệu sẽ bị xóa sau khi đóng tab. Nếu cần hỗ trợ về dữ liệu đã nhập, liên hệ nhóm phát triển và thống nhất một kênh riêng phù hợp trước khi cung cấp thông tin định danh. Không đăng thông tin cá nhân lên issue công khai.",
      },
    ],
  },
  "gioi-han": {
    intro:
      "Những số liệu và trạng thái trên website phục vụ kiểm thử phần mềm, không phải thông báo vận hành của một bãi đỗ thương mại.",
    sections: [
      {
        title: "Bãi đỗ và chỗ trống",
        text: "Danh sách bãi và ô phản ánh dữ liệu đang có trong môi trường thử nghiệm. Ô trống không đồng nghĩa với một chỗ đỗ đã được giữ. Hình bãi đỗ ở trang chủ chỉ là minh họa 2D, không phải camera trực tiếp.",
      },
      {
        title: "Nhận diện ảnh và trạng thái cảm biến",
        text: "Kết quả AI có thể sai và cần người dùng được phân quyền đối chiếu. Tín hiệu cảm biến và tình trạng phiên có thể khác nhau trong các tình huống kiểm thử; chúng không phải căn cứ tự động cho việc mở cổng ngoài đời.",
      },
      {
        title: "Phí, thanh toán và đặt chỗ",
        text: "Khoản phí hoặc trạng thái thanh toán trong dữ liệu thử nghiệm không phải hóa đơn hay yêu cầu thu tiền. Website không cung cấp một kênh thanh toán thương mại. Nội dung trình diễn không bảo đảm một yêu cầu đặt chỗ thực tế.",
      },
      {
        title: "Tính sẵn sàng",
        text: "Môi trường demo có thể tạm ngắt kết nối, bảo trì hoặc thay đổi cấu hình. Nếu gặp lỗi, ghi lại bước tái hiện và gửi báo lỗi đã che thông tin cá nhân; không thử lại bằng cách cung cấp thêm dữ liệu nhạy cảm.",
      },
    ],
  },
  "chong-gia-mao": {
    intro:
      "Smart-Parking trong website này là dự án mô phỏng. Hãy thận trọng nếu có người dùng tên hoặc hình ảnh dự án để yêu cầu tiền hay thông tin đăng nhập.",
    sections: [
      {
        title: "Không chuyển tiền vì lời mời từ bản demo",
        text: "Không chuyển khoản, đặt cọc, quét mã QR hoặc trả tiền giữ chỗ dựa trên số liệu, ảnh chụp hay lời giới thiệu của website này. Một yêu cầu thanh toán gắn với bản demo không chứng minh người gửi đại diện cho nhóm.",
      },
      {
        title: "Giữ riêng mật khẩu, OTP và token",
        text: "Không chia sẻ các mã này với bất kỳ người tự xưng là hỗ trợ viên nào. Chỉ nhập OTP bạn vừa yêu cầu vào đúng luồng xác minh trên website đang trải nghiệm.",
      },
      {
        title: "Kiểm tra nguồn trước khi thao tác",
        text: "Liên kết kho mã nguồn ở trang Liên hệ là kênh tham chiếu của dự án. Một trang sao chép logo, tên hoặc giao diện không tự động trở thành kênh của nhóm. Nếu nghi ngờ, dừng thao tác và gửi phản hồi đã ẩn thông tin nhạy cảm.",
      },
    ],
  },
  "lien-he": {
    intro:
      "Bạn có thể gửi góp ý, lỗi giao diện hoặc câu hỏi về phạm vi demo qua kho mã nguồn của dự án.",
    sections: [
      {
        title: "Báo lỗi để nhóm có thể tái hiện",
        text: "Nêu trang đang mở, vai trò tài khoản, các bước thao tác, kết quả mong đợi và lỗi quan sát được. Nếu đính kèm ảnh, hãy che email, biển số, khuôn mặt, token và các thông tin nhận diện khác.",
      },
      {
        title: "Trao đổi về dữ liệu cá nhân hoặc sự cố bảo mật",
        text: "Không đăng mật khẩu, OTP, khóa API, chuỗi kết nối database hay dữ liệu cá nhân lên issue công khai. Chỉ gửi mô tả chung để nhóm thống nhất kênh trao đổi riêng trước khi cung cấp chi tiết. Website chưa công bố email hỗ trợ hoặc số hotline riêng.",
      },
      {
        title: "Phạm vi hỗ trợ",
        text: "Nhóm tiếp nhận phản hồi cho phần mềm mô phỏng. Đây không phải kênh tìm xe, giữ chỗ, xử lý giao dịch hoặc hỗ trợ khẩn cấp cho một bãi đỗ thật.",
      },
    ],
  },
};
