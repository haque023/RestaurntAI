import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

// In development the YARP gateway is reached through this proxy, so the browser talks to a
// single origin and the backend needs no CORS configuration.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const gateway = env.VITE_GATEWAY_URL ?? "http://localhost:5080";

  return {
    plugins: [react()],
    server: {
      port: 3000,
      strictPort: true,
      proxy: { "/api": { target: gateway, changeOrigin: true } },
    },
    preview: { port: 3000 },
  };
});
