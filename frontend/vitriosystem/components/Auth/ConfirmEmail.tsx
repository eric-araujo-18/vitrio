"use client";

import { useEffect, useRef, useState } from "react";
import { CircleCheck, LoaderCircle } from "lucide-react";
import { confirmEmail } from "@/lib/api";
import { useAuth } from "@/lib/auth_context";
import { AuthLayout } from "./components/AuthLayout";
import { AuthError } from "./components/AuthFields";

type State = { kind: "loading" } | { kind: "done"; message: string } | { kind: "error"; message: string };

// Link "Confirmar e-mail" do cadastro de lojista. Confirma assim que a página abre.
export default function ConfirmEmail({ token }: { token: string }) {
  const { user, reloadUser } = useAuth();
  const [state, setState] = useState<State>(
    token ? { kind: "loading" } : { kind: "error", message: "Link inválido. Entre na sua conta e peça um novo." }
  );
  // Uma chamada só, mesmo se o efeito rodar duas vezes (modo de desenvolvimento do React).
  const started = useRef(false);
  // Só recarrega a conta se houver sessão: sem ela, o 401 mandaria para o login.
  const loggedIn = useRef(!!user);
  useEffect(() => {
    loggedIn.current = !!user;
  }, [user]);

  useEffect(() => {
    if (!token || started.current) return;
    started.current = true;
    confirmEmail(token)
      .then((res) => {
        if (!res.status) {
          setState({ kind: "error", message: res.mensagem ?? "Não foi possível confirmar o e-mail." });
          return;
        }
        setState({ kind: "done", message: res.mensagem ?? "E-mail confirmado." });
        // Logado neste navegador: o aviso do painel some sem precisar entrar de novo.
        if (loggedIn.current) void reloadUser();
      })
      .catch(() => setState({ kind: "error", message: "Não foi possível confirmar agora. Tente abrir o link de novo." }));
  }, [token, reloadUser]);

  const next = user ? { href: "/menu/stores", label: "Ir para minhas lojas" } : { href: "/auth/login", label: "Entrar" };

  return (
    <AuthLayout title="Confirmar e-mail" subtitle="Confirmamos que o e-mail da conta é seu." footer={null}>
      {state.kind === "loading" ? (
        <p role="status" className="flex items-center justify-center gap-2 py-4 text-body-md text-on-surface-variant">
          <LoaderCircle size={18} aria-hidden="true" className="animate-spin text-primary-container" />
          Confirmando...
        </p>
      ) : (
        <div className="flex flex-col gap-4">
          {state.kind === "done" ? (
            <div
              role="status"
              className="flex items-start gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-md text-emerald-800"
            >
              <CircleCheck size={18} aria-hidden="true" className="mt-px shrink-0" />
              <span>{state.message}</span>
            </div>
          ) : (
            <AuthError>{state.message}</AuthError>
          )}
          {/* Navegação completa (não <Link>): a conta é lida de novo, já com o e-mail confirmado. */}
          <a
            href={next.href}
            className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-primary-container text-title-md text-on-primary transition hover:brightness-105"
          >
            {next.label}
          </a>
        </div>
      )}
    </AuthLayout>
  );
}
