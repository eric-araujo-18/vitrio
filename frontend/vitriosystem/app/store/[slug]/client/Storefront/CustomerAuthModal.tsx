"use client";

import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { CircleAlert, LoaderCircle, X } from "lucide-react";
import { login, registerClient } from "@/lib/api";
import { useAuth } from "@/lib/auth_context";
import { formatPhone, isValidPhone, isvalidEmail } from "@/lib/validators";
import { iconButton, storeInput, storeOverlay, storePrimaryButton, useLockBodyScroll } from "./Ui";

/*
  Entrar / criar conta de cliente, dentro da própria vitrine (com as cores da loja).
  A conta é opcional: serve para preencher os dados no checkout e ver "Meus pedidos".
  A conta é do Vitrio, então o mesmo login funciona em qualquer loja.
*/

type Mode = "login" | "register";

interface CustomerAuthModalProps {
  storeName: string;
  initialMode?: Mode;
  onClose: () => void;
  /** Chamado depois de entrar ou criar a conta com sucesso. */
  onSuccess?: () => void;
}

export default function CustomerAuthModal({
  storeName,
  initialMode = "login",
  onClose,
  onSuccess,
}: CustomerAuthModalProps) {
  const { refresh } = useAuth();

  const [mode, setMode] = useState<Mode>(initialMode);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useLockBodyScroll();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !sending && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, sending]);

  function switchMode(next: Mode) {
    setMode(next);
    setError(null);
  }

  async function signIn(identifier: string) {
    const response = await login({ login: identifier, password });
    if (!response.status || !response.dados) {
      throw new Error(response.mensagem ?? "Não foi possível entrar.");
    }
    // login() guardou o token; refresh() carrega o usuário no contexto.
    await refresh();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    const cleanEmail = email.trim().toLowerCase();

    if (mode === "register") {
      if (name.trim().length < 2) return setError("Informe seu nome.");
      if (!isvalidEmail(cleanEmail)) return setError("Informe um e-mail válido.");
      if (phone && !isValidPhone(phone)) return setError("Telefone inválido. Use DDD + número.");
      if (password.length < 8) return setError("A senha precisa ter pelo menos 8 caracteres.");
    } else {
      if (!cleanEmail) return setError("Informe seu e-mail.");
      if (!password) return setError("Informe sua senha.");
    }

    setSending(true);
    try {
      if (mode === "register") {
        const created = await registerClient({
          name: name.trim(),
          email: cleanEmail,
          password,
          phone: phone || undefined,
        });
        if (!created.status) throw new Error(created.mensagem ?? "Não foi possível criar a conta.");
      }

      // Depois de criar a conta, já entra direto.
      await signIn(cleanEmail);
      onSuccess?.();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Algo deu errado. Tente de novo.");
    } finally {
      setSending(false);
    }
  }

  const isRegister = mode === "register";

  return (
    <div
      className={`${storeOverlay} flex items-end justify-center sm:items-center sm:p-4`}
      onClick={() => !sending && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="customer-auth-title"
        onClick={(e) => e.stopPropagation()}
        className="relative flex max-h-[92vh] w-full max-w-[420px] animate-modal-in flex-col gap-5 overflow-y-auto rounded-t-2xl bg-white p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom))] shadow-[0_25px_50px_-12px_rgba(15,23,42,0.25)] sm:rounded-2xl sm:p-6"
      >
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 id="customer-auth-title" className="text-headline-sm text-slate-900">
              {isRegister ? "Criar conta" : "Entrar"}
            </h2>
            <p className="mt-1 text-body-md text-slate-500">
              {isRegister
                ? "Seus dados ficam salvos para os próximos pedidos."
                : `Acompanhe seus pedidos na ${storeName}.`}
            </p>
          </div>
          <button type="button" onClick={onClose} disabled={sending} aria-label="Fechar" className={`${iconButton} -mt-1 -mr-2`}>
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        {/* Abas */}
        <div role="tablist" className="grid grid-cols-2 rounded-lg bg-slate-100 p-1">
          {(
            [
              ["login", "Entrar"],
              ["register", "Criar conta"],
            ] as [Mode, string][]
          ).map(([value, tabLabel]) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={mode === value}
              onClick={() => switchMode(value)}
              disabled={sending}
              className={`h-9 rounded-md text-label-md font-semibold transition-colors ${
                mode === value ? "bg-white text-slate-900 shadow-sm" : "text-slate-500 hover:text-slate-800"
              }`}
            >
              {tabLabel}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          {isRegister && (
            <Field id="ca-name" label="Nome">
              <input
                id="ca-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoComplete="name"
                maxLength={100}
                disabled={sending}
                className={`${storeInput} h-11`}
              />
            </Field>
          )}

          <Field id="ca-email" label="E-mail">
            <input
              id="ca-email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
              inputMode="email"
              disabled={sending}
              className={`${storeInput} h-11`}
            />
          </Field>

          {isRegister && (
            <Field id="ca-phone" label="Telefone / WhatsApp" hint="(opcional)">
              <input
                id="ca-phone"
                type="tel"
                value={phone}
                onChange={(e) => setPhone(formatPhone(e.target.value))}
                placeholder="(00) 00000-0000"
                inputMode="numeric"
                autoComplete="tel"
                maxLength={15}
                disabled={sending}
                className={`${storeInput} h-11`}
              />
            </Field>
          )}

          <Field id="ca-password" label="Senha" hint={isRegister ? "(mínimo 8 caracteres)" : undefined}>
            <input
              id="ca-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete={isRegister ? "new-password" : "current-password"}
              disabled={sending}
              className={`${storeInput} h-11`}
            />
          </Field>

          {error && (
            <div
              role="alert"
              className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
            >
              <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <button type="submit" disabled={sending} className={storePrimaryButton}>
            {sending && <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />}
            {sending ? "Aguarde..." : isRegister ? "Criar conta" : "Entrar"}
          </button>
        </form>

        <p className="text-center text-body-sm text-slate-500">
          Não precisa de conta para comprar. Ela só guarda seus dados e seus pedidos.
        </p>
      </div>
    </div>
  );
}

function Field({ id, label, hint, children }: { id: string; label: string; hint?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-label-md font-semibold text-slate-700">
        {label}
        {hint && <span className="ml-1 font-normal text-slate-400">{hint}</span>}
      </label>
      {children}
    </div>
  );
}