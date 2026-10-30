import { Logo } from "./Header.jsx";
import { Link } from "react-router-dom";
import { projectLinks } from "../content/projectInformation.js";
import "../home.css";

export default function Footer() {
  return (
    <footer className="footer">
      <div className="container footer__inner">
        <Logo small />
        <span>Dự án mô phỏng phục vụ học tập & trình diễn · OJT</span>
      </div>
      <div className="footer-resources">
        <div className="container">
          <div className="footer-resources__grid">
            <div className="footer-transparency">
              <span className="footer-eyebrow">Trải nghiệm có thông tin</span>
              <h2>Hiểu rõ trước khi bắt đầu.</h2>
              <p>
                Đây là một dự án mô phỏng, không phải dịch vụ giữ xe thương mại.
                Không chuyển tiền, đặt cọc hoặc dùng số liệu demo để đặt chỗ
                thực tế.
              </p>
              <Link to="/thong-tin/chong-gia-mao">
                Nhận biết yêu cầu giả mạo ↗
              </Link>
            </div>
            <nav
              className="footer-resource-links"
              aria-label="Chính sách và thông tin dự án"
            >
              {projectLinks.map((item) => (
                <Link to={`/thong-tin/${item.slug}`} key={item.slug}>
                  <span>
                    <strong>{item.label}</strong>
                    <small>{item.summary}</small>
                  </span>
                  <span aria-hidden="true">↗</span>
                </Link>
              ))}
            </nav>
          </div>
          <div className="footer-bottom">
            <span>Smart-Parking · Bản trình diễn phần mềm</span>
            <span>Không có dịch vụ thu tiền hoặc bảo đảm chỗ đỗ thực tế.</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
