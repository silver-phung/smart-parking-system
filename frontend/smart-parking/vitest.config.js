import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    include: ["src/**/*.ui.test.jsx", "src/services/staffApi.test.js"],
    setupFiles: ["./src/testSetup.js"],
    pool: "threads",
    poolOptions: { threads: { singleThread: true } },
    clearMocks: true,
  },
});
