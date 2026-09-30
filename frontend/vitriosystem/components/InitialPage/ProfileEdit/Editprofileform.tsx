"use client";

import { useState, type ChangeEvent, type FormEvent, type HTMLAttributes } from "react";
import {
  Check,
  CircleAlert,
  LoaderCircle,
  Mail,
  Phone,
  Save,
  User,
  type LucideIcon,
} from "lucide-react";
import { updateMyProfile } from "@/lib/api";
import { formatPhone, isvalidEmail, isValidPhone } from "@/lib/validators";

export interface ProfileData {
  name: string;
  email: string;
  phone: string;
}

interface EditProfileFormProps {
  initialData?: ProfileData;
  onUpdated?: (data: ProfileData) => void;
}

type Status = "idle" | "loading" | "success" | "error";
type FieldErrors = Partial<Record<keyof ProfileData, string>>;

export default function EditProfileForm({
  initialData = { name: "", email: "", phone: "" },
  onUpdated,
}: EditProfileFormProps) {
  const [form, setForm] = useState<ProfileData>(initialData);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [status, setStatus] = useState<Status>("idle");
  const [message, setMessage] = useState("");

  // Atualiza um campo, limpa o erro dele e esconde a mensagem anterior
  // (evita "Perfil atualizado" aparecendo enquanto o usuário já editou de novo).
  function updateField(field: keyof ProfileData, value: string) {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => ({ ...prev, [field]: undefined }));
    if (status === "success" || status === "error") setStatus("idle");
  }

  function validate() {
    const next: FieldErrors = {};
    if (!form.name.trim()) {
      next.name = "Informe seu nome.";
    }
    if (form.email.trim() && !isvalidEmail(form.email.trim())) {
      next.email = "Informe um e-mail válido.";
    }
    if (form.phone.trim() && !isValidPhone(form.phone.trim())) {
      next.phone = "Informe um telefone válido. Ex: (00) 00000-0000.";
    }
    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!validate()) return;

    setStatus("loading");
    setMessage("");

    try {
      // O backend ignora campos vazios; status=false vira exceção aqui.
      const res = await updateMyProfile({
        name: form.name.trim() || "",
        email: form.email.trim() || "",
        phone: form.phone.trim() || "",
      });

      if (!res.status) throw new Error(res.mensagem ?? "Erro ao atualizar perfil.");

      setStatus("success");
      setMessage(res.mensagem ?? "Perfil atualizado com sucesso.");
      onUpdated?.(form);
    } catch (err) {
      setStatus("error");
      setMessage(err instanceof Error ? err.message : "Erro ao atualizar perfil.");
    }
  }

  const isLoading = status === "loading";

  return (
    <section className="rounded-2xl border border-slate-200/85 bg-white/90 p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] backdrop-blur-md sm:p-7">
      <div className="mb-7">
        <h2 className="text-headline-md text-on-surface">Editar perfil</h2>
        <p className="mt-1 text-body-md text-on-surface-variant">Atualize seus dados de contato.</p>
      </div>

      {status === "success" && (
        <div
          role="status"
          className="mb-6 flex animate-[fadeIn_0.3s_ease] items-start gap-2.5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-body-md font-medium text-emerald-700"
        >
          <Check size={18} aria-hidden="true" className="mt-px shrink-0" />
          <span>{message}</span>
        </div>
      )}

      {status === "error" && (
        <div
          role="alert"
          className="mb-6 flex animate-[fadeIn_0.3s_ease] items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-body-md font-medium text-red-700"
        >
          <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
          <span>{message}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
        <Field
          id="name"
          label="Nome"
          icon={User}
          autoComplete="name"
          value={form.name}
          onChange={(e) => updateField("name", e.target.value)}
          placeholder="Seu nome completo"
          error={errors.name}
          disabled={isLoading}
        />

        <Field
          id="email"
          label="E-mail"
          icon={Mail}
          type="email"
          autoComplete="email"
          value={form.email}
          onChange={(e) => updateField("email", e.target.value)}
          placeholder="voce@exemplo.com"
          error={errors.email}
          disabled={isLoading}
        />

        <Field
          id="phone"
          label="Telefone"
          icon={Phone}
          type="tel"
          inputMode="numeric"
          autoComplete="tel"
          maxLength={15}
          value={form.phone}
          onChange={(e) => updateField("phone", formatPhone(e.target.value))}
          placeholder="(00) 00000-0000"
          error={errors.phone}
          disabled={isLoading}
        />

        <button
          type="submit"
          disabled={isLoading}
          className="mt-1 inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg border border-white/15 bg-linear-to-b from-primary-container to-[#1d4ed8] text-title-md text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] transition-all duration-200 hover:-translate-y-px hover:brightness-105 disabled:cursor-not-allowed disabled:opacity-65 disabled:hover:translate-y-0 disabled:hover:brightness-100 sm:w-auto sm:self-end sm:px-6"
        >
          {isLoading ? (
            <>
              <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
              Salvando...
            </>
          ) : (
            <>
              <Save size={18} aria-hidden="true" />
              Salvar alterações
            </>
          )}
        </button>
      </form>
    </section>
  );
}

/* ===========================
   CAMPO
=========================== */

interface FieldProps {
  id: string;
  label: string;
  icon: LucideIcon;
  value: string;
  onChange: (e: ChangeEvent<HTMLInputElement>) => void;
  placeholder: string;
  error?: string;
  disabled?: boolean;
  type?: string;
  inputMode?: HTMLAttributes<HTMLInputElement>["inputMode"];
  maxLength?: number;
  autoComplete?: string;
}

function Field({
  id,
  label,
  icon: Icon,
  value,
  onChange,
  placeholder,
  error,
  disabled,
  type = "text",
  inputMode,
  maxLength,
  autoComplete,
}: FieldProps) {
  const errorId = `${id}-error`;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-label-md font-semibold text-on-surface">
        {label}
      </label>

      <div className="group relative">
        <Icon
          size={18}
          aria-hidden="true"
          className={`pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 transition-colors ${
            error ? "text-red-500" : "text-outline group-focus-within:text-primary-container"
          }`}
        />
        <input
          id={id}
          type={type}
          inputMode={inputMode}
          maxLength={maxLength}
          autoComplete={autoComplete}
          value={value}
          onChange={onChange}
          placeholder={placeholder}
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className={`h-12 w-full rounded-lg border bg-white pr-4 pl-11 text-body-lg text-on-surface outline-none transition placeholder:text-slate-400 focus:ring-[3px] disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 ${
            error
              ? "border-red-300 focus:border-red-500 focus:ring-red-500/15"
              : "border-slate-300 focus:border-primary-container focus:ring-primary-container/15"
          }`}
        />
      </div>

      {error && (
        <p id={errorId} className="flex items-center gap-1.5 text-body-sm text-red-600">
          <CircleAlert size={14} aria-hidden="true" className="shrink-0" />
          {error}
        </p>
      )}
    </div>
  );
}