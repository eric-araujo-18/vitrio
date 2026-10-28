import Link from "next/link";
import { SearchX } from "lucide-react";

// Página 404 (endereço que não existe). A loja não encontrada tem tela própria, dentro da vitrine.
export default function NotFound() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-3 bg-surface px-4 text-center">
      <title>Página não encontrada | Vitrio</title>
      <div className="mb-1 flex h-16 w-16 items-center justify-center rounded-2xl bg-primary-fixed/50 text-primary">
        <SearchX size={32} aria-hidden="true" />
      </div>
      <h1 className="text-headline-md text-on-surface">Página não encontrada</h1>
      <p className="max-w-sm text-body-md text-on-surface-variant">
        O endereço pode ter sido digitado errado ou a página não existe mais.
      </p>
      <Link
        href="/"
        className="mt-2 inline-flex h-11 items-center rounded-lg bg-primary-container px-5 text-title-md text-white transition-colors hover:bg-[#1d4ed8]"
      >
        Ir para o início
      </Link>
    </main>
  );
}
