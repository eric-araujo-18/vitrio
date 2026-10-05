"use client";

import { useState, FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { IdCard, Lock, LogIn } from "lucide-react";
import { getMe, login, ROLES } from "@/lib/api";
import { formatCpf, isValidCpf } from "@/lib/validators";
import { useAuth, useGuestOnly } from "@/lib/auth_context";
import { AuthLayout } from "../Auth/components/AuthLayout";
import { AuthError, AuthInput, AuthSubmit } from "../Auth/components/AuthFields";

export default function Login() {
  const router = useRouter();
  const { refresh, logoutHere } = useAuth();

  const [cpf, setCpf] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { loading: guestOnlyLoading } = useGuestOnly();

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!isValidCpf(cpf)) {
      setError("Informe um CPF válido.");
      return;
    }

    setLoading(true);

    try {
      const response = await login({ login: cpf, password });

      if (!response.status || !response.dados) {
        setError(response.mensagem ?? "Não foi possível entrar.");
        return;
      }

      // Esta tela é do painel. Conta de cliente da vitrine não entra aqui.
      const me = await getMe();
      if (me.dados?.role === ROLES.CLIENT) {
        await logoutHere();
        setError("Esta é uma conta de cliente. Para comprar, entre pela página da loja.");
        return;
      }

      // login() já guardou o access token em memória; refresh() busca o usuário.
      await refresh();

      router.replace("/menu/initialpage");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao entrar.");
    } finally {
      setLoading(false);
    }
  }

  if (guestOnlyLoading) return null;

  return (
    <AuthLayout
      title="Entrar"
      subtitle="Acesse sua conta para continuar."
      footer={
        <>
          Não tem uma conta?{" "}
          <Link href="/auth/register" className="font-semibold text-primary hover:underline">
            Criar conta
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
        <AuthInput
          id="cpf"
          label="CPF"
          icon={IdCard}
          inputMode="numeric"
          autoComplete="username"
          placeholder="000.000.000-00"
          value={cpf}
          onChange={(e) => setCpf(formatCpf(e.target.value))}
          maxLength={14}
          required
        />

        <AuthInput
          id="password"
          label="Senha"
          icon={Lock}
          type="password"
          autoComplete="current-password"
          placeholder="Sua senha"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        <Link
          href="/auth/forgot-password"
          className="-mt-1 self-end text-label-md text-on-surface-variant transition-colors hover:text-primary hover:underline"
        >
          Esqueci minha senha
        </Link>

        {error && <AuthError>{error}</AuthError>}

        <AuthSubmit loading={loading} loadingText="Entrando...">
          <LogIn size={18} aria-hidden="true" />
          Entrar
        </AuthSubmit>
      </form>
    </AuthLayout>
  );
}