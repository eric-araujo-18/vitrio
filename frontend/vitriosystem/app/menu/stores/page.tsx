"use client";

import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import Storelist from "@/components/InitialPage/Storelist/Storelist";

export default function StoresPage() {
  return (
    <DashboardShell title="Lojas" subtitle="Crie novas lojas e acesse o painel de cada uma.">
      {() => <Storelist />}
    </DashboardShell>
  );
}