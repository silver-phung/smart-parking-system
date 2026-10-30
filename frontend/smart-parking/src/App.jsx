import { Navigate, Routes, Route, useLocation } from "react-router-dom";
import Header from "./components/Header.jsx";
import Footer from "./components/Footer.jsx";
import Home from "./pages/Home.jsx";
import Login from "./pages/Login.jsx";
import Register from "./pages/Register.jsx";
import CustomerDashboard from "./pages/CustomerDashboard.jsx";
import CustomerProfile from "./pages/CustomerProfile.jsx";
import RoleDashboard from "./pages/RoleDashboard.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import Viewer3D from "./pages/Viewer3D.jsx";
import RequireAuth from "./components/RequireAuth.jsx";

export default function App() {
  const { pathname } = useLocation();

  return (
    <>
      <Header />
      <main>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/customer" element={<CustomerDashboard />} />
          <Route path="/viewer-3d" element={<RequireAuth><Viewer3D /></RequireAuth>} />
          <Route path="/customer/profile" element={<CustomerProfile />} />
          <Route path="/admin" element={<AdminDashboard />} />
          <Route path="/manager" element={<RoleDashboard role="MANAGER" />} />
          <Route path="/staff" element={<RoleDashboard role="STAFF" />} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </main>
      {!["/admin", "/login", "/register"].includes(pathname) && <Footer />}
    </>
  );
}
