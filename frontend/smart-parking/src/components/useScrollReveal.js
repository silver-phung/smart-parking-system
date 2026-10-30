import { useEffect, useRef } from "react";

export default function useScrollReveal() {
  const ref = useRef(null);
  useEffect(() => {
    const nodes = [
      ...(ref.current?.querySelectorAll("[data-scroll-reveal]") || []),
    ];
    if (
      typeof IntersectionObserver === "undefined" ||
      window.matchMedia?.("(prefers-reduced-motion: reduce)").matches
    )
      return;
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.dataset.reveal = "visible";
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.08, rootMargin: "0px 0px -24px 0px" },
    );
    nodes.forEach((node) => {
      node.dataset.reveal = "pending";
      observer.observe(node);
    });
    return () => {
      observer.disconnect();
      nodes.forEach((node) => {
        delete node.dataset.reveal;
      });
    };
  }, []);
  return ref;
}
