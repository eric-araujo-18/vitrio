"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { MailCheck, Mail, Send } from "lucide-react";
import { forgotPassword } from "@/lib/api";
import { isvalidEmail } from "@/lib/validators";
import { AuthLayout } from "./components/AuthLayout";
import { AuthError, AuthInput, AuthSubmit } from "./components/AuthFields";

// storeSlug: veio da vitrine (cliente). O link do e-mail e o "voltar" levam para a loja.
export default function ForgotPassword({ storeSlug }: { storeSlug?: string }) {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sentMessage, setSentMessage] = useState<string | null>(null);

  const backHref = storeSlug ? `/store/${storeSlug}/client` : "/auth/login";
  const backLabel = storeSlug ? "Voltar para a loja" : "Voltar para o login";

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    const cleanEmail = email.trim();
    if (!isvalidEmail(cleanEmail)) {
      setError("Informe um e-mail válido.");
      return;
    }

    setLoading(true);
    try {
      const response = await forgotPassword(cleanEmail, storeSlug);
      if (!response.status) {
        setError(response.mensagem ?? "Não foi possível enviar o e-mail.");
        return;
      }
      setSentMessage(response.mensagem ?? "Se existir uma conta com esse e-mail, enviamos um link.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível enviar o e-mail.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout
      title="Esqueci minha senha"
      subtitle="Informe o e-mail da sua conta e enviaremos um link para criar uma nova senha."
      footer={
        <Link href={backHref} className="font-semibold text-primary hover:underline">
          {backLabel}
        </Link>
      }
    >
      {sentMessage ? (
        <div
          role="status"
          className="flex items-start gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-md text-emerald-800"
        >
          <MailCheck size={18} aria-hidden="true" className="mt-px shrink-0" />
          <span>{sentMessage} O link vale por 30 minutos.</span>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          <AuthInput
            id="email"
            label="E-mail"
            icon={Mail}
            type="email"
            autoComplete="email"
            placeholder="voce@exemplo.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />

          {error && <AuthError>{error}</AuthError>}

          <AuthSubmit loading={loading} loadingText="Enviando...">
            <Send size={18} aria-hidden="true" />
            Enviar link
          </AuthSubmit>
        </form>
      )}
    </AuthLayout>
  );
}
