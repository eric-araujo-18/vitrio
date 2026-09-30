"use client";

import { Globe } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import ComingSoon from "@/components/InitialPage/ComingSoon/ComingSoon";

export default function DomainsPage() {
  return (
    <DashboardShell title="Domínios">
      {() => (
        <ComingSoon
          icon={Globe}
          title="Domínio próprio"
          description="Em breve você poderá ligar um endereço como www.sualoja.com.br à sua vitrine. Por enquanto, use o link /store/sua-loja."
        />
      )}
    </DashboardShell>
  );
}