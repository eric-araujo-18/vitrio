import type { NextConfig } from "next";
import { PHASE_PRODUCTION_BUILD } from "next/constants";

// Cabeçalhos de segurança de todas as páginas:
// - frame-ancestors / X-Frame-Options: nenhum outro site pode embutir o painel num iframe (o golpe
//   de esconder um botão "Excluir loja" debaixo de outro clique);
// - nosniff: o navegador não "adivinha" o tipo de um arquivo;
// - Referrer-Policy: links para fora levam só o domínio, nunca o caminho (que pode ter um token,
//   como o de redefinir senha);
// - Permissions-Policy: o site não usa câmera, microfone nem localização.
// O HSTS só vale quando o site é aberto por HTTPS (em localhost, o navegador ignora).
const securityHeaders = [
  { key: "Content-Security-Policy", value: "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=()" },
  { key: "Strict-Transport-Security", value: "max-age=31536000" },
];

const nextConfig: NextConfig = {
  // O título e a prévia da vitrine dependem de uma consulta à API (app/store/[slug]/client/layout.tsx).
  // Por padrão o Next manda essas tags depois do início da página para navegadores e só espera por
  // elas para uma lista de robôs; com /.*/ ele espera para todos. Assim, a prévia funciona também em
  // apps fora da lista (Telegram, por exemplo), e o custo é só essa consulta, que fica em cache.
  htmlLimitedBots: /.*/,
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
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
    if (!process.env.NEXT_PUBLIC_SUPPORT_EMAIL?.trim()) {
      console.warn("\nAviso: NEXT_PUBLIC_SUPPORT_EMAIL vazia. O rodapé e a tela de Suporte não vão mostrar e-mail de contato.\n");
    }
    if (/^https?:\/\/(localhost|127\.0\.0\.1)(:|\/|$)/i.test(apiUrl)) {
      console.warn(
        `\nAviso: NEXT_PUBLIC_API_URL aponta para ${apiUrl}. Este build só funciona nesta máquina; para publicar, use o endereço público da API.\n`
      );
    }
  }
  return nextConfig;
}
