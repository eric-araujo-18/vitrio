// app/layout.tsx

import { AuthProvider } from "@/lib/auth_context";

import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Vitrio System",
  description: "Crie sua loja online e receba pedidos pela sua vitrine.",
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