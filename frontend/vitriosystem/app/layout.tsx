// app/layout.tsx

import { AuthProvider } from "@/lib/auth_context";

import type { Metadata } from "next";
import "./globals.css";

// O ícone da aba vem de app/icon.svg. A vitrine de cada loja troca o título e a prévia do link
// pelos da loja (app/store/[slug]/client/layout.tsx).
export const metadata: Metadata = {
  title: { default: "Vitrio: sua loja online", template: "%s | Vitrio" },
  description: "Crie sua loja online e receba pedidos pela sua vitrine.",
  applicationName: "Vitrio",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body>
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}