import type { ReactNode } from "react";
import { Clock, type LucideIcon } from "lucide-react";

interface ComingSoonProps {
  icon: LucideIcon;
  title: string;
  description: string;
  children?: ReactNode;
}

export default function ComingSoon({ icon: Icon, title, description, children }: ComingSoonProps) {
  return (
    <section className="flex max-w-[560px] flex-col items-center gap-3 rounded-2xl border border-slate-200/85 bg-white/90 px-6 py-12 text-center shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] backdrop-blur-md sm:px-8">
      <div className="mb-1 flex h-14 w-14 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
        <Icon size={28} aria-hidden="true" />
      </div>

      <h2 className="text-headline-md text-on-surface">{title}</h2>
      <p className="max-w-md text-body-lg text-on-surface-variant">{description}</p>

      <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-500/10 px-3 py-1 text-label-sm text-amber-700">
        <Clock size={12} aria-hidden="true" />
        Em breve
      </span>

      {children}
    </section>
  );
}