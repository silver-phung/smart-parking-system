import { Link, NavLink, useParams } from "react-router-dom";
import {
  projectInformation,
  projectLinks,
} from "../content/projectInformation.js";
import "../home.css";

export default function ProjectInformation() {
  const { topic } = useParams();
  const content = projectInformation[topic];
  const link = projectLinks.find((item) => item.slug === topic);
  if (!content || !link)
    return (
      <section className="container information-page">
        <h1>Không tìm thấy thông tin</h1>
        <Link to="/thong-tin/du-an">Về thông tin dự án</Link>
      </section>
    );
  return (
    <section className="container information-page">
      <div className="information-heading">
        <Link to="/" className="information-back">
          ← Trang chủ
        </Link>
        <span className="tag">Minh bạch về dự án</span>
        <h1>{link.label}</h1>
        <p>{content.intro}</p>
      </div>
      <div className="information-layout">
        <aside>
          <nav aria-label="Thông tin dự án">
            {projectLinks.map((item) => (
              <NavLink key={item.slug} to={`/thong-tin/${item.slug}`}>
                {item.label}
                <span aria-hidden="true">↗</span>
              </NavLink>
            ))}
          </nav>
          <p>Bản mô phỏng phục vụ học tập và trình diễn.</p>
        </aside>
        <article className="information-content">
          <div className="information-notice">
            <strong>Đây là một dự án mô phỏng.</strong>
            <p>
              Không phải dịch vụ giữ xe thương mại. Không chuyển tiền hoặc đặt
              cọc để sử dụng bản demo.
            </p>
          </div>
          {content.sections.map((section) => (
            <section key={section.title}>
              <h2>{section.title}</h2>
              <p>{section.text}</p>
            </section>
          ))}
          {topic === "lien-he" && (
            <div className="information-contact">
              <a
                className="btn btn--primary"
                href="https://github.com/silver-phung/smart-parking-system/issues"
                target="_blank"
                rel="noopener noreferrer"
              >
                Góp ý / báo lỗi trên GitHub ↗
              </a>
              <a
                href="https://github.com/silver-phung/smart-parking-system"
                target="_blank"
                rel="noopener noreferrer"
              >
                Xem kho mã nguồn dự án ↗
              </a>
              <p>
                GitHub có thể yêu cầu đăng nhập. Chỉ gửi nội dung đã che thông
                tin nhạy cảm.
              </p>
            </div>
          )}
          <div className="information-bottom">
            <Link to="/parking">Khám phá bãi đỗ demo →</Link>
            <Link to="/thong-tin/lien-he">Liên hệ nhóm phát triển →</Link>
          </div>
        </article>
      </div>
    </section>
  );
}
