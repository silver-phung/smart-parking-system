import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, MemoryRouter } from "react-router-dom";
import App from "./App.jsx";
import "./index.css";

// Khi chạy trong Claude (artifact) thì không có URL thật, dùng MemoryRouter.
// Chạy bình thường (npm run dev) vẫn dùng BrowserRouter.
const Router = import.meta.env.VITE_EMBED ? MemoryRouter : BrowserRouter;

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <Router basename={import.meta.env.VITE_EMBED ? undefined : "/app"}>
      <App />
    </Router>
  </React.StrictMode>
);
