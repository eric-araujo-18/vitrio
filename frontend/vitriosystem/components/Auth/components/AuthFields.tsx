"use client";

import { useState, type InputHTMLAttributes, type ReactNode } from "react";
import { CircleAlert, Eye, EyeOff, LoaderCircle, type LucideIcon } from "lucide-react";

/* ===========================
   CAMPO COM ÍCONE
   Se type="password", ganha o botão de mostrar/ocultar.
=========================== */

type AuthInputProps = InputHTMLAttributes<HTMLInputElement> & {
  id: string;
  label: string;
  icon: LucideIcon;
  hint?: string;
};

export function AuthInput({ id, label, icon: Icon, hint, type = "text", ...rest }: AuthInputProps) {
  const [showPassword, setShowPassword] = useState(false);
  const isPassword = type === "password";

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-label-md text-on-surface">
        {label}
        {hint && <span className="ml-1 font-normal text-outline">{hint}</span>}
      </label>

      <div className="group relative">
        <Icon
          size={18}
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline transition-colors group-focus-within:text-primary-container"
        />

        <input
          id={id}
          type={isPassword && showPassword ? "text" : type}
          className={`h-11 w-full rounded-lg border border-slate-300 bg-white pl-10 text-body-lg text-on-surface outline-none transition placeholder:text-slate-400 focus:border-primary-container focus:ring-[3px] focus:ring-primary-container/15 ${
            isPassword ? "pr-11" : "pr-3"
          }`}
          {...rest}
        />

        {isPassword && (
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
            className="absolute top-1/2 right-1.5 -translate-y-1/2 rounded-md p-1.5 text-outline transition-colors hover:bg-surface-container-low hover:text-on-surface"
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        )}
      </div>
    </div>
  );
}

/* ===========================
   MENSAGEM DE ERRO
=========================== */

export function AuthError({ children }: { children: ReactNode }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
    >
      <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
      <span>{children}</span>
    </div>
  );
}

/* ===========================
   BOTÃO DE ENVIO
=========================== */

type AuthSubmitProps = {
  loading: boolean;
  loadingText: string;
  children: ReactNode;
};

export function AuthSubmit({ loading, loadingText, children }: AuthSubmitProps) {
  return (
    <button
      type="submit"
      disabled={loading}
      className="mt-2 inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg border border-white/15 bg-linear-to-b from-primary-container to-[#1d4ed8] text-title-md text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] transition-all duration-200 hover:-translate-y-px hover:brightness-105 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:brightness-100"
    >
      {loading ? (
        <>
          <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
          {loadingText}
        </>
      ) : (
        children
      )}
    </button>
  );
}