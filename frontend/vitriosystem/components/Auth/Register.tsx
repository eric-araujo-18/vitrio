"use client";

import { useState, FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { IdCard, Lock, Mail, Phone, User, UserPlus } from "lucide-react";
import { register } from "@/lib/api";
import { formatCpf, isValidCpf, formatPhone, isValidPhone } from "@/lib/validators";
import { useGuestOnly } from "@/lib/auth_context";
import { AuthLayout } from "../Auth/components/AuthLayout";
import { AuthError, AuthInput, AuthSubmit } from "../Auth/components/AuthFields";

export default function Register() {
  const router = useRouter();

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [cpf, setCpf] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { loading: guestOnlyLoading } = useGuestOnly();

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError("Informe seu nome.");
      return;
    }

    if (password.length < 8) {
      setError("A senha precisa ter pelo menos 8 caracteres.");
      return;
    }

    if (!cpf.trim()) {
      setError("Informe o CPF");
      return;
    }

    if (phone.trim() && !isValidPhone(phone.trim())) {
      setError("Telefone inválido. Use o formato (00) 00000-0000.");
      return;
    }

    if (cpf.trim() && !isValidCpf(cpf)) {
      setError("CPF inválido. Confira os números digitados.");
      return;
    }

    setLoading(true);

    try {
      const response = await register({
        name,
        email,
        password,
        phone: phone || undefined,
        cpf: cpf.trim(), // já validado acima e agora é obrigatório na API
      });

      // Assim como no login, o backend responde 200 OK mesmo quando a
      // regra de negócio falha (CPF/e-mail já cadastrado, CPF inválido
      // na revalidação do servidor, etc) — quem indica isso é "status",
      // não o HTTP status. Sem essa checagem o erro passava batido.
      if (!response.status) {
        setError(response.mensagem ?? "Não foi possível criar a conta.");
        return;
      }

      // RegisterAsync não gera token — só cria o usuário. Por isso aqui
      // manda pro login em vez de já autenticar direto.
      router.push("/auth/login");
    } catch (err) {
      // Chega aqui só em falha de rede/servidor, não em regra de negócio
      // (essa já foi tratada acima).
      setError(err instanceof Error ? err.message : "Erro ao criar conta.");
    } finally {
      setLoading(false);
    }
  }

  if (guestOnlyLoading) return null;

  return (
    <AuthLayout
      title="Criar sua conta"
      subtitle="Cadastre-se para começar a usar o Vitrio System."
      footer={
        <>
          Já tem uma conta?{" "}
          <Link href="/auth/login" className="font-semibold text-primary hover:underline">
            Entrar
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
        <AuthInput
          id="name"
          label="Seu nome"
          icon={User}
          autoComplete="name"
          placeholder="Como devemos te chamar?"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
        />

        <AuthInput
          id="email"
          label="E-mail"
          icon={Mail}
          type="email"
          autoComplete="email"
          placeholder="voce@exemplo.com"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />

        <AuthInput
          id="password"
          label="Senha"
          icon={Lock}
          type="password"
          autoComplete="new-password"
          placeholder="Mínimo de 8 caracteres"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          minLength={8}
        />

        <AuthInput
          id="phone"
          label="Telefone"
          hint="(opcional)"
          icon={Phone}
          type="tel"
          inputMode="numeric"
          autoComplete="tel"
          placeholder="(00) 00000-0000"
          value={phone}
          onChange={(e) => setPhone(formatPhone(e.target.value))}
          maxLength={15}
        />

        <AuthInput
          id="cpf"
          label="CPF"
          icon={IdCard}
          inputMode="numeric"
          placeholder="000.000.000-00"
          value={cpf}
          onChange={(e) => setCpf(formatCpf(e.target.value))}
          maxLength={14}
          required
        />

        {error && <AuthError>{error}</AuthError>}

        <AuthSubmit loading={loading} loadingText="Criando...">
          <UserPlus size={18} aria-hidden="true" />
          Criar conta
        </AuthSubmit>
      </form>
    </AuthLayout>
  );
}