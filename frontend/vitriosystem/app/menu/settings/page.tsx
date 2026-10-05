"use client";

import { useState, type FormEvent } from "react";
import {
  Check,
  CircleAlert,
  Eye,
  EyeOff,
  KeyRound,
  LoaderCircle,
  Lock,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import { changePassword, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth_context";

export default function SettingsPage() {
  return (
    <DashboardShell title="Configurações" subtitle="Segurança da sua conta.">
      {() => <ChangePasswordForm />}
    </DashboardShell>
  );
}

// Tempo para o usuário ler a mensagem de sucesso antes de sair.
const LOGOUT_DELAY_MS = 2500;

function ChangePasswordForm() {
  const { logout } = useAuth();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const busy = loading || success;
  const confirmMatches = confirm.length > 0 && confirm === next;

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (next.length < 8) return setError("A nova senha precisa ter pelo menos 8 caracteres.");
    if (next !== confirm) return setError("A confirmação não confere com a nova senha.");

    setLoading(true);
    try {
      await unwrap(changePassword({ currentPassword: current, newPassword: next }));
      setSuccess(true);
      // O backend revoga todas as sessões; aqui encerramos a atual também,
      // depois de dar tempo de ler a mensagem.
      setTimeout(() => {
        void logout();
      }, LOGOUT_DELAY_MS);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao alterar senha.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="max-w-2xl rounded-2xl border border-slate-200/85 bg-white/90 p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] backdrop-blur-md sm:p-7">
      <div className="mb-7 flex items-start gap-3.5">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
          <ShieldCheck size={20} aria-hidden="true" />
        </div>
        <div>
          <h2 className="text-headline-md text-on-surface">Alterar senha</h2>
          <p className="mt-1 text-body-md text-on-surface-variant">
            Por segurança, todas as suas sessões serão encerradas.
          </p>
        </div>
      </div>

      {success && (
        <div
          role="status"
          className="mb-6 flex animate-[fadeIn_0.3s_ease] items-start gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-md font-medium text-emerald-700"
        >
          <Check size={18} aria-hidden="true" className="mt-px shrink-0" />
          <span>Senha alterada. Você será redirecionado para entrar com a nova senha.</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
        <PasswordField
          id="current"
          label="Senha atual"
          icon={Lock}
          value={current}
          onChange={setCurrent}
          autoComplete="current-password"
          disabled={busy}
        />

        <PasswordField
          id="next"
          label="Nova senha"
          icon={KeyRound}
          value={next}
          onChange={setNext}
          autoComplete="new-password"
          disabled={busy}
          hint="Mínimo de 8 caracteres."
        />

        <PasswordField
          id="confirm"
          label="Confirmar nova senha"
          icon={KeyRound}
          value={confirm}
          onChange={setConfirm}
          autoComplete="new-password"
          disabled={busy}
          hint={confirmMatches ? "As senhas conferem." : undefined}
          hintSuccess={confirmMatches}
        />

        {error && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-body-md font-medium text-red-700"
          >
            <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <button
          type="submit"
          disabled={busy || !current || !next}
          className="mt-1 inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg border border-white/15 bg-linear-to-b from-primary-container to-[#1d4ed8] text-title-md text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] transition-all duration-200 hover:-translate-y-px hover:brightness-105 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:brightness-100 sm:w-auto sm:self-end sm:px-6"
        >
          {loading ? (
            <>
              <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
              Salvando...
            </>
          ) : (
            <>
              <KeyRound size={18} aria-hidden="true" />
              Alterar senha
            </>
          )}
        </button>
      </form>
    </section>
  );
}

/* ===========================
   CAMPO DE SENHA
   Com botão de mostrar/ocultar e dica opcional.
=========================== */

interface PasswordFieldProps {
  id: string;
  label: string;
  icon: LucideIcon;
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  disabled?: boolean;
  hint?: string;
  hintSuccess?: boolean;
}

function PasswordField({
  id,
  label,
  icon: Icon,
  value,
  onChange,
  autoComplete,
  disabled,
  hint,
  hintSuccess,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const hintId = `${id}-hint`;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-label-md font-semibold text-on-surface">
        {label}
      </label>

      <div className="group relative">
        <Icon
          size={18}
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-outline transition-colors group-focus-within:text-primary-container"
        />
        <input
          id={id}
          type={visible ? "text" : "password"}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          autoComplete={autoComplete}
          disabled={disabled}
          aria-describedby={hint ? hintId : undefined}
          className="h-12 w-full rounded-lg border border-slate-300 bg-white pr-12 pl-11 text-body-lg text-on-surface outline-none transition placeholder:text-slate-400 focus:border-primary-container focus:ring-[3px] focus:ring-primary-container/15 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          disabled={disabled}
          aria-label={visible ? "Ocultar senha" : "Mostrar senha"}
          className="absolute top-1/2 right-1.5 -translate-y-1/2 rounded-md p-2 text-outline transition-colors hover:bg-surface-container-low hover:text-on-surface disabled:cursor-not-allowed"
        >
          {visible ? <EyeOff size={18} /> : <Eye size={18} />}
        </button>
      </div>

      {hint && (
        <p
          id={hintId}
          className={`flex items-center gap-1.5 text-body-sm ${
            hintSuccess ? "text-emerald-700" : "text-outline"
          }`}
        >
          {hintSuccess && <Check size={14} aria-hidden="true" className="shrink-0" />}
          {hint}
        </p>
      )}
    </div>
  );
}