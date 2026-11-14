import { Link, Routes, Route, useLocation } from "react-router-dom";
import { useEffect, useSyncExternalStore } from "react";
import ParkingScene from "./pages/ParkingScene.jsx";
import ParkingSlotManagement from "./pages/ParkingSlotManagement.jsx";
import RequireRole from "./components/RequireRole.jsx";
import { getAuthSession, subscribeAuthSession } from "./services/authSession.js";
import "./app-shell.css";
import Header from "./components/Header.jsx";
import Footer from "./components/Footer.jsx";
import Home from "./pages/Home.jsx";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import CustomerDashboard from "./pages/CustomerDashboard.jsx";
import CustomerProfile from "./pages/CustomerProfile.jsx";
import ManagerWorkspace from "./pages/ManagerWorkspace.jsx";
import ManagerStaffAssignments from "./pages/ManagerStaffAssignments.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import AdminParkingSettings from "./pages/AdminParkingSettings.jsx";
import AdminPolicies from "./pages/AdminPolicies.jsx";
import StaffWorkspace from "./pages/StaffWorkspace.jsx";
import AdminPeople from "./pages/AdminPeople.jsx";
import CustomerSessions from "./pages/CustomerSessions.jsx";
import ProjectInformation from "./pages/ProjectInformation.jsx";
import CashHandovers from "./pages/CashHandovers.jsx";
import ShiftManagement from "./pages/ShiftManagement.jsx";

export default function App() {
  const { pathname, hash } = useLocation();
  const session = useSyncExternalStore(subscribeAuthSession, getAuthSession, getAuthSession);
  const workspace = ["/admin", "/manager", "/staff", "/customer", "/parking"].some(p => pathname.startsWith(p));
  const authPage = pathname === "/login" || pathname === "/register";
  useEffect(() => {
    if (hash)
      document
        .getElementById(hash.slice(1))
        ?.scrollIntoView({ block: "start" });
    else window.scrollTo?.(0, 0);
  }, [pathname, hash]);

  return (
    <div className={`app-frame ${workspace ? "app-frame--workspace" : authPage ? "app-frame--auth" : ""}`}>
      <Header workspace={workspace} />
      <main>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/thong-tin/:topic" element={<ProjectInformation />} />
          <Route path="/customer" element={<CustomerDashboard />} />
          <Route path="/parking" element={<ParkingScene />} />
          <Route path="/parking/3d" element={<ParkingScene />} />
          <Route path="/parking/slots" element={<RequireRole role={["ADMIN", "MANAGER"]}>{session?.userProfile.role === "ADMIN" ? <AdminPolicies /> : <ParkingSlotManagement role={session?.userProfile.role} />}</RequireRole>} />
          <Route path="/customer/sessions" element={<CustomerSessions />} />
          <Route path="/customer/profile" element={<CustomerProfile />} />
          <Route path="/admin" element={<AdminDashboard />} />
          <Route path="/admin/parking-settings" element={<AdminParkingSettings />} />
          <Route path="/admin/policies" element={<AdminPolicies />} />
          <Route path="/admin/people" element={<AdminPeople />} />
          <Route path="/manager" element={<ManagerWorkspace />} />
          <Route path="/handovers" element={<RequireRole role={["MANAGER", "STAFF"]}><CashHandovers /></RequireRole>} />
          <Route path="/manager/shifts" element={<RequireRole role="MANAGER"><ShiftManagement /></RequireRole>} />
          <Route path="/manager/assignments" element={<ManagerStaffAssignments />} />
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
      {!workspace && !authPage && (
          <Footer />
        )}
    </div>
  );
}
