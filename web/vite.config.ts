import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5174,
    strictPort: true,
    // In development the UI calls /api on its own origin and Vite forwards it to the ASP.NET Core API.
    proxy: {
      "/api": "http://localhost:5120",
      "/health": "http://localhost:5120",
    },
  },
  test: {
    environment: "jsdom",
  },
});
