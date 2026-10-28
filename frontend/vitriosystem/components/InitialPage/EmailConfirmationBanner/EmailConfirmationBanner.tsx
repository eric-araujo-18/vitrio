"use client";

import { useState } from "react";
import { LoaderCircle, MailWarning } from "lucide-react";
import { resendEmailConfirmation, type User } from "@/lib/api";

// Lojista com o e-mail ainda não confirmado (cadastro novo ou e-mail trocado no perfil). Até
// confirmar, ele não cria loja nem assina plano, e os avisos de pedido não vão para esse e-mail.
export default function EmailConfirmationBanner({ user }: { user: User }) {
  const [sending, setSending] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  async function handleResend() {
    setSending(true);
    setMessage(null);
    try {
      const res = await resendEmailConfirmation();
      setMessage({ ok: res.status, text: res.mensagem ?? (res.status ? "Link enviado." : "Não foi possível enviar.") });
    } catch {
      setMessage({ ok: false, text: "Não foi possível enviar agora. Tente de novo em instantes." });
    } finally {
      setSending(false);
    }
  }

  return (
    <div
      role="status"
      className="mb-6 flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-900 sm:flex-row sm:items-center sm:justify-between"
    >
      <div className="flex items-start gap-3">
        <MailWarning size={20} aria-hidden="true" className="mt-0.5 shrink-0" />
        <div className="text-body-md">
          <p className="font-semibold">Confirme seu e-mail</p>
          <p>
            Enviamos um link para <strong>{user.email}</strong>. Até você confirmar, não dá para criar lojas nem assinar
            planos. Confira também a caixa de spam.
          </p>
          {message && <p className={`mt-1 ${message.ok ? "text-emerald-800" : "text-red-700"}`}>{message.text}</p>}
        </div>
      </div>
      <button
        type="button"
        onClick={handleResend}
        disabled={sending}
        className="inline-flex h-10 shrink-0 items-center justify-center gap-2 rounded-lg border border-amber-300 bg-white px-4 text-label-md font-semibold text-amber-900 transition-colors hover:bg-amber-100 disabled:opacity-60"
      >
        {sending && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
        Reenviar link
      </button>
    </div>
  );
}
