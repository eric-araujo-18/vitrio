"use client";

import { Bell } from "lucide-react";
import { useAuth } from "@/lib/auth_context";

const ROLE_LABELS: Record<string, string> = {
  Client: "Cliente",
  Admin: "Administrador",
  Shopkeeper: "Lojista",
};

function getInitials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

export default function Header() {
  const { user } = useAuth();

  return (
    <header className="sticky top-0 z-40 flex h-20 items-center justify-end border-b border-slate-200/85 bg-white/85 px-5 backdrop-blur-xl sm:px-10">
      <div className="flex min-w-0 items-center gap-4 sm:gap-6">
        {/* TODO: ligar a notificações quando o recurso existir */}
        <button
          type="button"
          aria-label="Notificações"
          title="Notificações"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-on-surface-variant shadow-[0_1px_2px_rgba(15,23,42,0.04)] transition-colors hover:border-slate-300 hover:bg-surface hover:text-primary-container"
        >
          <Bell size={18} aria-hidden="true" />
        </button>

        <div className="h-8 w-px shrink-0 bg-slate-200" aria-hidden="true" />

        <div className="group flex min-w-0 items-center gap-3">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-linear-to-b from-primary-container to-[#1d4ed8] text-title-md font-bold text-on-primary shadow-[0_2px_8px_rgba(37,99,235,0.25)] transition-transform duration-200 group-hover:scale-105 group-hover:rotate-6">
            {user ? getInitials(user.name) : ""}
          </div>

          {user ? (
            <div className="min-w-0">
              <strong className="block max-w-[140px] truncate text-title-md text-on-surface sm:max-w-[220px]">
                {user.name}
              </strong>
              <span className="block text-body-sm text-on-surface-variant">
                {ROLE_LABELS[user.role] ?? user.role}
              </span>
            </div>
          ) : (
            // Esqueleto enquanto o usuário carrega
            <div className="flex flex-col gap-1.5" aria-label="Carregando usuário">
              <span className="h-3.5 w-28 animate-pulse rounded bg-slate-200" />
              <span className="h-3 w-16 animate-pulse rounded bg-slate-100" />
            </div>
          )}
        </div>
      </div>
    </header>
  );
}