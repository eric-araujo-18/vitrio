"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { CircleCheck, KeyRound, Lock } from "lucide-react";
import { resetPassword } from "@/lib/api";
import { AuthLayout } from "./components/AuthLayout";
import { AuthError, AuthInput, AuthSubmit } from "./components/AuthFields";

const MIN_LENGTH = 8;

// token: veio no link do e-mail. storeSlug: o pedido foi feito pela vitrine (cliente).
export default function ResetPassword({ token, storeSlug }: { token: string; storeSlug?: string }) {
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const nextHref = storeSlug ? `/store/${storeSlug}/client` : "/auth/login";
  const nextLabel = storeSlug ? "Voltar para a loja e entrar" : "Ir para o login";

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (password.length < MIN_LENGTH) {
      setError(`A nova senha precisa ter pelo menos ${MIN_LENGTH} caracteres.`);
      return;
    }
    if (password !== confirm) {
      setError("A confirmação não confere com a nova senha.");
      return;
    }

    setLoading(true);
    try {
      const response = await resetPassword(token, password);
      if (!response.status) {
        setError(response.mensagem ?? "Não foi possível redefinir a senha.");
        return;
      }
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível redefinir a senha.");
    } finally {
      setLoading(false);
    }
  }

  const forgotHref = storeSlug ? `/auth/forgot-password?store=${storeSlug}` : "/auth/forgot-password";

  return (
    <AuthLayout
      title="Criar nova senha"
      subtitle="Escolha uma senha nova para a sua conta."
      footer={
        done ? null : (
          <Link href={forgotHref} className="font-semibold text-primary hover:underline">
            Pedir um novo link
          </Link>
        )
      }
    >
      {!token ? (
        <AuthError>Link inválido. Peça um novo em &quot;Esqueci minha senha&quot;.</AuthError>
      ) : done ? (
        <div className="flex flex-col gap-4">
          <div
            role="status"
            className="flex items-start gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-md text-emerald-800"
          >
            <CircleCheck size={18} aria-hidden="true" className="mt-px shrink-0" />
            <span>Senha alterada. Por segurança, as sessões abertas foram encerradas.</span>
          </div>
          <Link
            href={nextHref}
            className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-primary-container text-title-md text-on-primary transition hover:brightness-105"
          >
            {nextLabel}
          </Link>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <AuthInput
            id="password"
            label="Nova senha"
            hint={`(mínimo ${MIN_LENGTH} caracteres)`}
            icon={KeyRound}
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />

          <AuthInput
            id="confirm"
            label="Confirme a nova senha"
            icon={Lock}
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            required
          />

          {error && <AuthError>{error}</AuthError>}

          <AuthSubmit loading={loading} loadingText="Salvando...">
            Salvar nova senha
          </AuthSubmit>
        </form>
      )}
    </AuthLayout>
  );
}
