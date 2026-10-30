import { Link, useLocation } from "react-router-dom";
export default function AnchorLink({ id, className, children }) {
  const { pathname, hash } = useLocation();
  return (
    <Link
      to={`/#${id}`}
      className={className}
      onClick={() => {
        if (pathname === "/" && hash === `#${id}`)
          document
            .getElementById(id)
            ?.scrollIntoView({ behavior: "smooth", block: "start" });
      }}
    >
      {children}
    </Link>
  );
}
