import type { NextConfig } from "next";
import { PHASE_PRODUCTION_BUILD } from "next/constants";

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

// No build de produção, a URL da API vai junto no código do navegador (variáveis NEXT_PUBLIC_*
// são fixadas no build). Sem ela, o site publicado chamaria http://localhost:5020 (o padrão de
// lib/api.tsx) e não funcionaria para ninguém, sem nenhum erro no build. Por isso o build recusa.
// O .env.local já foi lido quando esta função roda.
export default function config(phase: string): NextConfig {
  if (phase === PHASE_PRODUCTION_BUILD) {
    const apiUrl = process.env.NEXT_PUBLIC_API_URL?.trim();
    if (!apiUrl) {
      throw new Error(
        "Defina NEXT_PUBLIC_API_URL com o endereço público da API (ex.: https://api.seudominio.com.br) antes do build."
      );
    }
    if (/^https?:\/\/(localhost|127\.0\.0\.1)(:|\/|$)/i.test(apiUrl)) {
      console.warn(
        `\nAviso: NEXT_PUBLIC_API_URL aponta para ${apiUrl}. Este build só funciona nesta máquina; para publicar, use o endereço público da API.\n`
      );
    }
  }
  return nextConfig;
}
