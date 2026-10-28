"use client";

import { CircleHelp, Mail } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import ComingSoon from "@/components/InitialPage/ComingSoon/ComingSoon";
import { SUPPORT_EMAIL } from "@/lib/site";

export default function SupportPage() {
  return (
    <DashboardShell title="Suporte">
      {() => (
        <ComingSoon
          icon={CircleHelp}
          title="Central de ajuda"
          description={
            SUPPORT_EMAIL
              ? "Estamos montando uma central com tutoriais. Enquanto isso, fale com a gente por e-mail."
              : "Estamos montando uma central com tutoriais."
          }
        >
          {SUPPORT_EMAIL && (
            <a
              href={`mailto:${SUPPORT_EMAIL}`}
              className="mt-2 inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-5 text-title-md text-primary shadow-[0_1px_2px_rgba(15,23,42,0.04)] transition-colors hover:border-slate-300 hover:bg-surface"
            >
              <Mail size={18} aria-hidden="true" />
              {SUPPORT_EMAIL}
            </a>
          )}
        </ComingSoon>
      )}
    </DashboardShell>
  );
}