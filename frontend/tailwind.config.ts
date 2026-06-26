import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        // Design system palette from MASTER.md
        background: "#0F172A",   // slate-900
        surface: "#1E293B",      // slate-800
        muted: "#334155",        // slate-700
        primary: "#0369A1",      // sky-700
        "primary-light": "#0EA5E9", // sky-500
        foreground: "#F8FAFC",   // slate-50
        "foreground-muted": "#94A3B8", // slate-400
        accent: "#020617",       // slate-950
        success: "#16A34A",
        warning: "#D97706",
        error: "#DC2626",
      },
      fontFamily: {
        sans: ['"Plus Jakarta Sans"', "system-ui", "sans-serif"],
      },
      screens: {
        xs: "375px",
        sm: "640px",
        md: "768px",
        lg: "1024px",
        xl: "1280px",
      },
    },
  },
  plugins: [],
};

export default config;
