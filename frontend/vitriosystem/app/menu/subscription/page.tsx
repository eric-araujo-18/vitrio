"use client";

import { CreditCard } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import ComingSoon from "@/components/InitialPage/ComingSoon/ComingSoon";

export default function SubscriptionPage() {
  return (
    <DashboardShell title="Assinatura">
      {() => (
        <ComingSoon
          icon={CreditCard}
          title="Planos e cobrança"
          description="Durante o período de lançamento, todos os recursos do Vitrio estão liberados sem custo."
        />
      )}
    </DashboardShell>
  );
}