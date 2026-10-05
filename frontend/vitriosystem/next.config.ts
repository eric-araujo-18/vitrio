import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async redirects() {
    return [
      // Link curto da vitrine (/store/minha-loja), o que o lojista divulga.
      // A vitrine em si fica em /store/minha-loja/client.
      {
        source: "/store/:slug",
        destination: "/store/:slug/client",
        permanent: false,
      },
    ];
  },
};

export default nextConfig;
