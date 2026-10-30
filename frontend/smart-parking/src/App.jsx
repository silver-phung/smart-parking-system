import { Link, Routes, Route, useLocation } from "react-router-dom";
import { useEffect } from "react";
import Header from "./components/Header.jsx";
import Footer from "./components/Footer.jsx";
import Home from "./pages/Home.jsx";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import CustomerDashboard from "./pages/CustomerDashboard.jsx";
import CustomerProfile from "./pages/CustomerProfile.jsx";
import ManagerWorkspace from "./pages/ManagerWorkspace.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import StaffWorkspace from "./pages/StaffWorkspace.jsx";
import AdminPeople from "./pages/AdminPeople.jsx";
import CustomerSessions from "./pages/CustomerSessions.jsx";

export default function App() {
  const { pathname, hash } = useLocation();
  useEffect(() => {
    if (hash)
      document
        .getElementById(hash.slice(1))
        ?.scrollIntoView({ block: "start" });
    else window.scrollTo?.(0, 0);
  }, [pathname, hash]);

  return (
    <>
      <Header />
      <main>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/customer" element={<CustomerDashboard />} />
          <Route path="/parking" element={<CustomerDashboard publicView />} />
          <Route path="/customer/sessions" element={<CustomerSessions />} />
          <Route path="/customer/profile" element={<CustomerProfile />} />
          <Route path="/admin" element={<AdminDashboard />} />
          <Route path="/admin/people" element={<AdminPeople />} />
          <Route path="/manager" element={<ManagerWorkspace />} />
          <Route path="/staff" element={<StaffWorkspace />} />
          <Route
            path="*"
            element={
              <section className="workspace container">
                <div className="workspace-panel workspace-empty">
                  <h1>Không tìm thấy trang</h1>
                  <p>Đường dẫn này không còn khả dụng.</p>
                  <Link className="btn btn--primary" to="/">
                    Về trang chủ
                  </Link>
                </div>
              </section>
            }
          />
        </Routes>
      </main>
      {!pathname.startsWith("/admin") &&
        !["/manager", "/staff", "/login", "/register"].includes(pathname) && (
          <Footer />
        )}
    </>
  );
}
