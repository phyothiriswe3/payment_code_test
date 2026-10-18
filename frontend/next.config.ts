import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Pages read searchParams/params at request time; Cache Components/PPR were
  // left on by the scaffold and caused DYNAMIC_SERVER_USAGE 500s at runtime.
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
