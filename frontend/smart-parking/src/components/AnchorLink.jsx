import { useLocation, useNavigate } from "react-router-dom";

// Cuộn mượt tới một mục trên trang chủ. Nếu đang ở trang khác (login/register)
// thì quay về trang chủ rồi mới cuộn.
export default function AnchorLink({ id, className, children }) {
  const navigate = useNavigate();
  const { pathname } = useLocation();

  const go = (e) => {
    e.preventDefault();
    const scroll = () =>
      document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
    if (pathname !== "/") {
      navigate("/");
      setTimeout(scroll, 80);
    } else {
      scroll();
    }
  };

  return (
    <a href={`/#${id}`} className={className} onClick={go}>
      {children}
    </a>
  );
}
