import Link from "next/link";
import type { ReactNode } from "react";
import { ArrowLeft, Store } from "lucide-react";

type AuthLayoutProps = {
  title: string;
  subtitle: string;
  children: ReactNode;
  footer: ReactNode;
};

export function AuthLayout({ title, subtitle, children, footer }: AuthLayoutProps) {
  return (
    <div className="relative isolate flex min-h-screen items-center justify-center overflow-hidden bg-surface px-4 py-10 text-on-surface antialiased">
      {/* Brilhos de fundo, os mesmos da home */}
      <div className="pointer-events-none absolute top-[-160px] left-1/2 -z-10 h-[420px] w-[820px] -translate-x-1/2 rounded-full bg-linear-to-tr from-primary-fixed/50 via-secondary-fixed/30 to-primary/10 blur-[120px]" />
      <div className="pointer-events-none absolute -right-40 bottom-0 -z-10 h-80 w-80 rounded-full bg-secondary-container/15 blur-3xl" />

      <div className="w-full max-w-[420px]">
        <Link
          href="/"
          className="mb-6 inline-flex items-center gap-1.5 text-label-md text-on-surface-variant transition-colors hover:text-primary"
        >
          <ArrowLeft size={16} aria-hidden="true" />
          Voltar para o início
        </Link>

        <div className="rounded-2xl border border-slate-200/85 bg-white/90 p-8 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] backdrop-blur-md sm:p-10">
          <Link href="/" className="inline-flex items-center gap-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-on-primary">
              <Store size={20} aria-hidden="true" />
            </span>
            <span className="text-headline-sm font-bold tracking-tight text-on-surface">
              Vitrio<span className="text-primary">System</span>
            </span>
          </Link>

          <h1 className="mt-8 mb-1 text-2xl font-bold tracking-tight text-on-surface">{title}</h1>
          <p className="mb-6 text-body-md text-on-surface-variant">{subtitle}</p>

          {children}
        </div>

        <p className="mt-6 text-center text-body-md text-on-surface-variant">{footer}</p>
      </div>
    </div>
  );
}