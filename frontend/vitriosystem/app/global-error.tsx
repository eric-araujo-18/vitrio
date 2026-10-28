"use client";

import { useEffect } from "react";

// Erro no layout principal: esta página substitui o documento inteiro e não recebe o CSS do site,
// por isso os estilos são inline.
export default function GlobalError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <html lang="pt-BR">
      <body
        style={{
          margin: 0,
          minHeight: "100vh",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: 12,
          padding: 16,
          textAlign: "center",
          fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
          background: "#f8f9ff",
          color: "#0b1c30",
        }}
      >
        <title>Algo deu errado | Vitrio</title>
        <h1 style={{ margin: 0, fontSize: 24 }}>Algo deu errado</h1>
        <p style={{ margin: 0, maxWidth: 380, color: "#434655" }}>
          Não conseguimos carregar o site agora. Tente de novo em instantes.
        </p>
        <button
          type="button"
          onClick={() => retry()}
          style={{
            marginTop: 8,
            height: 44,
            padding: "0 20px",
            border: 0,
            borderRadius: 8,
            background: "#2563eb",
            color: "#fff",
            fontSize: 16,
            cursor: "pointer",
          }}
        >
          Tentar de novo
        </button>
      </body>
    </html>
  );
}
