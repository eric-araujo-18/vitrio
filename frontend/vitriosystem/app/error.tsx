"use client";

import { useEffect } from "react";
import Link from "next/link";
import { RotateCcw, TriangleAlert } from "lucide-react";

// Erro inesperado numa página (o layout continua). O detalhe vai só para o console.
export default function Error({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-3 bg-surface px-4 text-center">
      <title>Algo deu errado | Vitrio</title>
      <div className="mb-1 flex h-16 w-16 items-center justify-center rounded-2xl bg-amber-100 text-amber-700">
        <TriangleAlert size={32} aria-hidden="true" />
      </div>
      <h1 className="text-headline-md text-on-surface">Algo deu errado</h1>
      <p className="max-w-sm text-body-md text-on-surface-variant">
        Não conseguimos mostrar esta página agora. Tente de novo em instantes.
      </p>
      <div className="mt-2 flex flex-wrap justify-center gap-2">
        <button
          type="button"
          onClick={() => retry()}
          className="inline-flex h-11 items-center gap-2 rounded-lg bg-primary-container px-5 text-title-md text-white transition-colors hover:bg-[#1d4ed8]"
        >
          <RotateCcw size={18} aria-hidden="true" />
          Tentar de novo
        </button>
        <Link
          href="/"
          className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-5 text-title-md text-on-surface transition-colors hover:bg-surface-container-low"
        >
          Ir para o início
        </Link>
      </div>
    </main>
  );
}
