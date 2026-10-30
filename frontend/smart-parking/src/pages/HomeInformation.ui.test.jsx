import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import Home from "./Home.jsx";
import Footer from "../components/Footer.jsx";
import ProjectInformation from "./ProjectInformation.jsx";
import { projectLinks } from "../content/projectInformation.js";
import { clearAuthSession } from "../services/authSession.js";

let instances;
beforeEach(() => {
  clearAuthSession();
  instances = [];
  vi.spyOn(globalThis, "fetch").mockImplementation(async (url) =>
    Response.json(
      url.includes("health")
        ? { status: "Healthy" }
        : { status: "Online", service: "Smart Parking API" },
    ),
  );
  vi.stubGlobal(
    "IntersectionObserver",
    class {
      constructor(callback) {
        this.callback = callback;
        this.observe = vi.fn();
        this.unobserve = vi.fn();
        this.disconnect = vi.fn();
        instances.push(this);
      }
    },
  );
  vi.stubGlobal(
    "matchMedia",
    vi.fn(() => ({ matches: false })),
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  clearAuthSession();
});
const mountHome = () =>
  render(
    <MemoryRouter
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      <Home />
    </MemoryRouter>,
  );
it("reveals observed Home blocks once and disconnects on navigation", async () => {
  const mounted = mountHome();
  await screen.findByText("Đang hoạt động");
  const observer = instances[0];
  const node = observer.observe.mock.calls[0][0];
  expect(node.dataset.reveal).toBe("pending");
  act(() => observer.callback([{ target: node, isIntersecting: true }]));
  expect(node.dataset.reveal).toBe("visible");
  expect(observer.unobserve).toHaveBeenCalledWith(node);
  mounted.unmount();
  expect(observer.disconnect).toHaveBeenCalled();
});
it("keeps all content visible for reduced motion", async () => {
  globalThis.matchMedia.mockReturnValue({ matches: true });
  const { container } = mountHome();
  await screen.findByText("Đang hoạt động");
  expect(instances).toHaveLength(0);
  expect(container.querySelector('[data-reveal="pending"]')).toBeNull();
});
it("keeps content readable when IntersectionObserver is unavailable", async () => {
  vi.stubGlobal("IntersectionObserver", undefined);
  const { container } = mountHome();
  await screen.findByText("Đang hoạt động");
  expect(container.querySelector('[data-reveal="pending"]')).toBeNull();
  expect(
    screen.getByRole("link", { name: "Xem bãi đỗ" }).getAttribute("href"),
  ).toBe("/parking");
  expect(screen.queryByText("Xác nhận đặt chỗ")).toBeNull();
});
it.each(projectLinks)(
  "opens $label from the public footer",
  ({ slug, label }) => {
    render(
      <MemoryRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <Routes>
          <Route path="/" element={<Footer />} />
          <Route path="/thong-tin/:topic" element={<ProjectInformation />} />
        </Routes>
      </MemoryRouter>,
    );
    fireEvent.click(
      screen.getByRole("link", {
        name: new RegExp(label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")),
      }),
    );
    expect(screen.getByRole("heading", { level: 1, name: label })).toBeTruthy();
    expect(screen.getByText("Đây là một dự án mô phỏng.")).toBeTruthy();
    expect(
      screen.getByRole("link", { name: /Trang chủ/ }).getAttribute("href"),
    ).toBe("/");
    if (slug === "lien-he")
      expect(
        screen
          .getByRole("link", { name: /Góp ý \/ báo lỗi trên GitHub/ })
          .getAttribute("href"),
      ).toBe("https://github.com/silver-phung/smart-parking-system/issues");
  },
);
it("handles an unknown information topic without crashing", () => {
  render(
    <MemoryRouter initialEntries={["/thong-tin/constructor"]}>
      <Routes>
        <Route path="/thong-tin/:topic" element={<ProjectInformation />} />
      </Routes>
    </MemoryRouter>,
  );
  expect(
    screen.getByRole("heading", { name: "Không tìm thấy thông tin" }),
  ).toBeTruthy();
});
