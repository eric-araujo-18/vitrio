"use client";

import { LogOut } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import EditProfileForm from "@/components/InitialPage/ProfileEdit/EditProfileForm";
import { useAuth } from "@/lib/auth_context";
import { formatPhone } from "@/lib/validators";

export default function MyAccountPage() {
  const { reloadUser, logout } = useAuth();

  return (
    <DashboardShell title="Minha conta ✍️" subtitle="Edite suas informações de contato.">
      {(user) => (
        <div className="flex max-w-2xl flex-col gap-6">
          <EditProfileForm
            // Antes o formulário abria vazio; agora vem preenchido com os dados atuais.
            initialData={{ name: user.name, email: user.email, phone: formatPhone(user.phone ?? "") }}
            onUpdated={reloadUser}
          />

          {/* No celular a sidebar vira barra embaixo e não tem botão de sair,
              então ele aparece aqui só nessa largura. */}
          <button
            type="button"
            onClick={logout}
            className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg border border-red-200 bg-white text-title-md text-red-600 transition-colors hover:bg-red-50 hover:text-red-700 min-[577px]:hidden"
          >
            <LogOut size={18} aria-hidden="true" />
            Sair da conta
          </button>
        </div>
      )}
    </DashboardShell>
  );
}