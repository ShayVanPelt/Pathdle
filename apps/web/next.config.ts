import type { NextConfig } from "next";
import { loadEnvConfig } from "@next/env";
import path from "path";

// Load monorepo root .env (cwd is apps/web when Next runs).
loadEnvConfig(path.join(process.cwd(), "../.."));

const nextConfig: NextConfig = {};

export default nextConfig;
