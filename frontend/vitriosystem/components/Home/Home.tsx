"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowRight,
  BadgeCheck,
  Boxes,
  Check,
  CircleCheck,
  ClipboardList,
  CreditCard,
  Handshake,
  Images,
  MessageCircle,
  Package,
  Palette,
  QrCode,
  Rocket,
  Share2,
  Smartphone,
  Store,
  Truck,
  UserPlus,
  Wallet,
} from "lucide-react";
import { useGuestOnly } from "@/lib/auth_context";
import { getPlans, type Plan } from "@/lib/api_subscription";
import { formatPrice } from "@/lib/format";
import { SUPPORT_EMAIL } from "@/lib/site";

/* ===========================
   DADOS DA PÁGINA
   Deixar o conteúdo em arrays facilita editar
   textos sem mexer no layout.
   Tudo aqui descreve o que o sistema faz hoje: não prometa recurso,
   integração, número de clientes ou certificação que não exista
   (o Código de Defesa do Consumidor proíbe propaganda enganosa).
   Os planos vêm da API (GET /api/Subscription/plans).
=========================== */

const navLinks = [
  { label: "Recursos", href: "#recursos" },
  { label: "Como funciona", href: "#como-funciona" },
  { label: "Planos", href: "#planos" },
  { label: "Contato", href: "#contato" },
];

const heroPerks = [
  { icon: BadgeCheck, text: "Plano grátis, sem prazo para acabar" },
  { icon: CreditCard, text: "Não pede cartão no cadastro" },
  { icon: Smartphone, text: "Vitrine pensada para o celular" },
];

const integrations = [
  { icon: QrCode, label: "Pix", color: "text-emerald-600" },
  { icon: CreditCard, label: "Cartão de crédito", color: "text-primary" },
  { icon: Handshake, label: "Mercado Pago", color: "text-sky-600" },
  { icon: MessageCircle, label: "WhatsApp", color: "text-emerald-700" },
];

const features = [
  {
    icon: Palette,
    tag: "VITRINE",
    iconBox: "bg-primary/10 text-primary",
    tagColor: "text-primary",
    title: "Uma vitrine com a cara da sua loja",
    text: "Cada loja ganha um link próprio, com o seu logo e as suas cores. Compartilhado no WhatsApp ou no Instagram, o link aparece com o nome e o logo da loja.",
    showOrderTracker: false,
  },
  {
    icon: Package,
    tag: "PRODUTOS",
    iconBox: "bg-secondary/10 text-secondary",
    tagColor: "text-secondary",
    title: "Produtos completos",
    text: "Várias fotos por produto, tamanhos com estoque separado, a mesma peça em várias cores, preço promocional, destaques e categorias com subcategorias.",
    showOrderTracker: false,
  },
  {
    icon: ClipboardList,
    tag: "PEDIDOS",
    iconBox: "bg-secondary-fixed/50 text-secondary",
    tagColor: "text-secondary",
    title: "Pedidos organizados",
    text: "O cliente compra sem precisar criar conta. O pedido aparece no painel e no seu e-mail, e você avança cada etapa até a entrega. O resumo da loja mostra os pedidos e o faturamento dos últimos 30 dias.",
    showOrderTracker: true,
  },
  {
    icon: Boxes,
    tag: "ESTOQUE",
    iconBox: "bg-primary/10 text-primary",
    tagColor: "text-primary",
    title: "Estoque sempre certo",
    text: "Cada pedido baixa o estoque na hora, sem vender a mesma peça duas vezes. Se o pedido for cancelado, o estoque volta sozinho. Produto esgotado aparece como esgotado na vitrine.",
    showOrderTracker: false,
  },
  {
    icon: Wallet,
    tag: "PAGAMENTO",
    iconBox: "bg-secondary/10 text-secondary",
    tagColor: "text-secondary",
    title: "Pix e cartão no checkout",
    text: "Conecte sua conta do Mercado Pago e receba por Pix ou cartão: o dinheiro cai direto na sua conta. Cancelou um pedido pago? O valor volta para o cliente pelo próprio Mercado Pago. Nos planos pagos; no grátis, você combina o pagamento com o cliente.",
    showOrderTracker: false,
  },
  {
    icon: Truck,
    tag: "ENTREGA",
    iconBox: "bg-secondary-fixed/50 text-secondary",
    tagColor: "text-secondary",
    title: "Entrega do seu jeito",
    text: "Cobre uma taxa fixa, dê entrega grátis a partir de um valor ou combine com cada cliente. O carrinho mostra a regra antes de o cliente fechar o pedido.",
    showOrderTracker: false,
  },
];

// As mesmas etapas do painel (Pendente → Confirmado → Enviado → Entregue).
const orderSteps = [
  { label: "Pendente", status: "done" },
  { label: "Confirmado", status: "done" },
  { label: "Enviado", status: "current" },
  { label: "Entregue", status: "pending" },
] as const;

const steps = [
  {
    icon: UserPlus,
    iconBox: "bg-primary/10 text-primary",
    title: "1. Crie a conta e a loja",
    text: "Cadastre-se com nome, e-mail e CPF, confirme o e-mail e escolha o nome, o logo e as cores da loja.",
  },
  {
    icon: Images,
    iconBox: "bg-secondary/10 text-secondary",
    title: "2. Cadastre os produtos",
    text: "Fotos, preço, estoque, tamanhos e cores. As fotos são reduzidas automaticamente para a vitrine abrir rápido no celular.",
  },
  {
    icon: Share2,
    iconBox: "bg-secondary-fixed/50 text-secondary",
    title: "3. Divulgue o link e venda",
    text: "Mande o link da loja no WhatsApp, no Instagram ou onde quiser. Os pedidos chegam no painel e por e-mail.",
  },
];

const ctaPerks = [
  "Não pede cartão no cadastro",
  "Plano grátis sem prazo para acabar",
  "Cancele a assinatura quando quiser",
];

// Só links que levam a algum lugar. Termos de uso e política de privacidade entram aqui quando
// as páginas existirem.
const footerColumns = [
  {
    title: "Vitrio",
    links: [
      { label: "Recursos", href: "#recursos" },
      { label: "Como funciona", href: "#como-funciona" },
      { label: "Planos", href: "#planos" },
    ],
  },
  {
    title: "Sua conta",
    links: [
      { label: "Criar conta", href: "/auth/register" },
      { label: "Entrar", href: "/auth/login" },
      { label: "Esqueci minha senha", href: "/auth/forgot-password" },
    ],
  },
];

/* ===========================
   COMPONENTES AUXILIARES
=========================== */

function Logo() {
  return (
    <Link href="/" className="group flex items-center gap-2">
      {/* Troque por <img src="/logo.svg" ... /> quando tiver o logo em /public */}
      <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-on-primary transition-transform duration-200 group-hover:scale-105">
        <Store size={20} aria-hidden="true" />
      </span>
      <span className="text-headline-sm font-bold tracking-tight text-on-surface">
        Vitrio<span className="text-primary">System</span>
      </span>
    </Link>
  );
}

function SectionHeading({
  eyebrow,
  eyebrowClass,
  title,
  text,
}: {
  eyebrow: string;
  eyebrowClass: string;
  title: string;
  text: string;
}) {
  return (
    <div className="mx-auto mb-12 flex max-w-3xl flex-col items-center text-center">
      <span className={`mb-2 text-label-sm font-bold uppercase ${eyebrowClass}`}>{eyebrow}</span>
      <h2 className="mb-2 text-display-lg-mobile text-on-surface md:text-display-lg">{title}</h2>
      <p className="text-body-lg text-on-surface-variant">{text}</p>
    </div>
  );
}

function OrderTracker() {
  return (
    <div className="flex items-center justify-between gap-1 rounded-xl bg-surface-bright p-4 text-center">
      {orderSteps.map((step, i) => (
        <div key={step.label} className="contents">
          <div
            className={`flex flex-1 flex-col items-center ${
              step.status === "pending" ? "opacity-60" : ""
            }`}
          >
            <span
              className={`mb-1 flex h-6 w-6 items-center justify-center rounded-full text-[11px] font-bold ${
                step.status === "done"
                  ? "bg-emerald-500 text-white"
                  : step.status === "current"
                    ? "bg-primary text-white"
                    : "bg-surface-container text-outline"
              }`}
            >
              {step.status === "done" ? <Check size={14} strokeWidth={3} /> : i + 1}
            </span>
            <span
              className={`whitespace-nowrap text-[11px] font-semibold ${
                step.status === "current"
                  ? "text-primary"
                  : step.status === "pending"
                    ? "text-outline"
                    : "text-on-surface"
              }`}
            >
              {step.label}
            </span>
          </div>

          {i < orderSteps.length - 1 && (
            <div
              className={`mb-4 h-0.5 w-6 shrink-0 ${
                orderSteps[i + 1].status === "done"
                  ? "bg-emerald-500"
                  : orderSteps[i + 1].status === "current"
                    ? "bg-primary"
                    : "bg-surface-container"
              }`}
            />
          )}
        </div>
      ))}
    </div>
  );
}

// O que cada plano inclui, a partir dos limites que vêm da API.
function planItems(plan: Plan): string[] {
  return [
    plan.maxStores === 1 ? "1 loja" : `Até ${plan.maxStores} lojas`,
    plan.maxProductsPerStore == null
      ? "Produtos ilimitados"
      : `Até ${plan.maxProductsPerStore} produtos por loja`,
    plan.allowsOnlinePayment
      ? "Pix e cartão pelo Mercado Pago"
      : "Pagamento combinado com o cliente",
    "Pedidos, estoque e entrega no painel",
  ];
}

/** Planos e preços lidos da API, os mesmos da tela de Assinatura. */
function PlansSection({ cardClass }: { cardClass: string }) {
  const [plans, setPlans] = useState<Plan[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    getPlans()
      .then((res) => {
        if (!active) return;
        if (res.status && res.dados) setPlans(res.dados);
        else setFailed(true);
      })
      .catch(() => active && setFailed(true));
    return () => {
      active = false;
    };
  }, []);

  return (
    <section id="planos" className="w-full scroll-mt-20 bg-surface py-12">
      <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
        <SectionHeading
          eyebrow="Planos"
          eyebrowClass="rounded-full bg-primary-fixed/40 px-3 py-1 text-on-primary-fixed-variant"
          title="Comece grátis e mude quando precisar"
          text="Todos os planos têm vitrine, pedidos, estoque e entrega. Os pagos aumentam os limites e liberam o pagamento online."
        />

        {failed ? (
          <p className="text-center text-body-md text-on-surface-variant">
            Não foi possível carregar os planos agora. Você também vê todos eles em Assinatura, depois de criar a
            conta.
          </p>
        ) : (
          <div className="mx-auto grid max-w-5xl grid-cols-1 gap-6 md:grid-cols-3">
            {plans === null
              ? [0, 1, 2].map((i) => (
                  <div key={i} className={`${cardClass} h-80 animate-pulse`} aria-hidden="true" />
                ))
              : plans.map((plan) => (
                  <article key={plan.code} className={`${cardClass} flex flex-col`}>
                    <h3 className="text-title-md font-bold text-on-surface">{plan.name}</h3>
                    {plan.description && (
                      <p className="mt-1 text-body-sm text-on-surface-variant">{plan.description}</p>
                    )}
                    <p className="mt-4 mb-5 text-on-surface">
                      {plan.priceMonthly > 0 ? (
                        <>
                          <span className="text-headline-md font-bold">{formatPrice(plan.priceMonthly)}</span>
                          <span className="text-body-md text-on-surface-variant"> por mês</span>
                        </>
                      ) : (
                        <span className="text-headline-md font-bold">Grátis</span>
                      )}
                    </p>
                    <ul className="mb-6 flex flex-col gap-2 text-body-sm text-on-surface">
                      {planItems(plan).map((item) => (
                        <li key={item} className="flex items-start gap-2">
                          <Check size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-emerald-600" />
                          {item}
                        </li>
                      ))}
                    </ul>
                    <Link
                      href="/auth/register"
                      className="mt-auto inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-5 text-label-md font-semibold text-primary transition-colors hover:border-slate-300 hover:bg-surface"
                    >
                      Começar
                    </Link>
                  </article>
                ))}
          </div>
        )}

        <p className="mx-auto mt-6 max-w-3xl text-center text-body-sm text-on-surface-variant">
          A assinatura é mensal, paga pelo Mercado Pago. Cancele quando quiser: o plano vale até o fim do período já
          pago. As vendas pagas online têm as tarifas do Mercado Pago.
        </p>
      </div>
    </section>
  );
}

/* ===========================
   PÁGINA
=========================== */

export default function Home() {
  const { loading: guestOnlyLoading } = useGuestOnly();

  if (guestOnlyLoading) return null;

  const cardClass =
    "rounded-2xl bg-surface-container-lowest p-8 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-xl";

  return (
    <div className="bg-surface text-body-md text-on-surface antialiased selection:bg-primary selection:text-on-primary">
      {/* HEADER */}
      <header className="fixed inset-x-0 top-0 z-50 bg-surface-container-lowest/85 shadow-[0_1px_8px_rgba(11,28,48,0.04)] backdrop-blur-xl">
        <div className="mx-auto flex h-20 max-w-[1600px] items-center justify-between gap-6 px-4 sm:px-8">
          <Logo />

          <nav className="hidden items-center gap-8 lg:flex">
            {navLinks.map((link) => (
              <a
                key={link.href}
                href={link.href}
                className="py-1 text-label-md text-on-surface-variant transition-colors hover:text-on-surface"
              >
                {link.label}
              </a>
            ))}
          </nav>

          <div className="flex items-center gap-4">
            <Link
              href="/auth/login"
              className="hidden h-10 items-center justify-center rounded-lg px-4 text-label-md text-on-surface-variant transition-all duration-200 hover:bg-surface-container-low hover:text-primary sm:inline-flex"
            >
              Login
            </Link>
            <Link
              href="/auth/register"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-primary px-6 text-label-md text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] transition-all duration-200 hover:bg-primary-container hover:shadow-[0_4px_12px_rgba(37,99,235,0.35)]"
            >
              Criar Conta
            </Link>
          </div>
        </div>
      </header>

      <main className="w-full pt-20">
        {/* HERO */}
        <section className="relative isolate w-full overflow-hidden bg-linear-to-b from-surface-container-low/60 via-surface to-surface py-12">
          {/* Brilhos de fundo */}
          <div className="pointer-events-none absolute top-[-120px] left-1/2 -z-10 h-[480px] w-[980px] -translate-x-1/2 rounded-full bg-linear-to-tr from-primary-fixed/40 via-secondary-fixed/30 to-primary/10 blur-[130px]" />
          <div className="pointer-events-none absolute top-1/3 -left-48 -z-10 h-96 w-96 rounded-full bg-primary/5 blur-3xl" />
          <div className="pointer-events-none absolute top-1/2 -right-48 -z-10 h-96 w-96 rounded-full bg-secondary-container/15 blur-3xl" />

          <div className="mx-auto max-w-[1600px] px-4 pt-8 sm:px-8 lg:pt-12">
            <div className="mx-auto flex max-w-4xl flex-col items-center text-center">
              <h1 className="mb-4 max-w-3xl text-display-lg-mobile text-on-surface md:text-display-lg">
                Crie e gerencie sua loja de forma{" "}
                <span className="bg-linear-to-r from-primary via-primary-container to-secondary bg-clip-text text-transparent">
                  simples
                </span>
                .
              </h1>

              <p className="mb-8 max-w-2xl text-body-lg text-on-surface-variant">
                Monte a vitrine da sua loja, cadastre produtos com fotos, tamanhos e cores, e receba pedidos pelo
                link que você divulga no WhatsApp e no Instagram. O cliente paga por Pix ou cartão, ou combina
                direto com você.
              </p>

              <div className="flex w-full flex-col items-center gap-4 sm:w-auto sm:flex-row">
                <Link
                  href="/auth/register"
                  className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-primary px-8 text-title-md text-on-primary shadow-[0_2px_4px_rgba(0,74,198,0.2),0_8px_20px_rgba(37,99,235,0.3)] transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary-container sm:w-auto"
                >
                  Criar minha loja grátis
                  <ArrowRight size={20} aria-hidden="true" />
                </Link>
              </div>

              <div className="mt-6 flex flex-wrap items-center justify-center gap-6 pt-1 text-label-sm text-on-surface-variant">
                {heroPerks.map((perk) => (
                  <span key={perk.text} className="inline-flex items-center gap-1.5">
                    <perk.icon size={17} className="text-secondary" aria-hidden="true" />
                    {perk.text}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* PAGAMENTO E CONTATO */}
        <section className="w-full bg-surface-container-lowest py-8 shadow-sm">
          <div className="mx-auto flex max-w-[1600px] flex-col items-center justify-center gap-4 px-4 text-center sm:px-8">
            <p className="text-label-md font-semibold tracking-wider text-outline uppercase">
              Seus clientes pagam e falam com você pelo que já usam
            </p>

            <div className="flex flex-wrap items-center justify-center gap-x-8 gap-y-4 pt-1">
              {integrations.map((item) => (
                <div
                  key={item.label}
                  className="flex items-center gap-2 rounded-xl bg-surface-bright px-4 py-2 shadow-sm transition-transform hover:scale-105"
                >
                  <item.icon size={22} className={item.color} aria-hidden="true" />
                  <span className="text-title-md font-bold text-on-surface">{item.label}</span>
                </div>
              ))}
            </div>

            <p className="text-body-sm text-on-surface-variant">
              Pix e cartão pelo Mercado Pago, nos planos pagos. O botão do WhatsApp leva o cliente direto para a
              conversa com a loja.
            </p>
          </div>
        </section>

        {/* RECURSOS */}
        <section id="recursos" className="w-full scroll-mt-20 bg-surface py-12">
          <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
            <SectionHeading
              eyebrow="Recursos"
              eyebrowClass="rounded-full bg-primary-fixed/40 px-3 py-1 text-on-primary-fixed-variant"
              title="O que sua loja ganha com o Vitrio"
              text="Da vitrine ao pedido entregue: produtos, estoque, pagamento e entrega no mesmo painel."
            />

            <div className="grid grid-cols-1 gap-6 md:grid-cols-2 xl:grid-cols-3">
              {features.map((feature) => (
                <article key={feature.tag} className={`${cardClass} flex flex-col justify-between`}>
                  <div>
                    <div className="mb-4 flex items-center justify-between">
                      <div
                        className={`flex h-12 w-12 items-center justify-center rounded-xl ${feature.iconBox}`}
                      >
                        <feature.icon size={28} aria-hidden="true" />
                      </div>
                      <span className={`font-mono text-code-sm font-bold ${feature.tagColor}`}>
                        {feature.tag}
                      </span>
                    </div>
                    <h3 className="mb-1 text-headline-md font-bold text-on-surface">
                      {feature.title}
                    </h3>
                    <p className="mb-6 text-body-md text-on-surface-variant">{feature.text}</p>
                  </div>

                  {feature.showOrderTracker && <OrderTracker />}
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* COMO FUNCIONA */}
        <section id="como-funciona" className="w-full scroll-mt-20 bg-surface-container-low/40 py-12">
          <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
            <SectionHeading
              eyebrow="Como funciona"
              eyebrowClass="text-secondary"
              title="Sua loja no ar em três passos"
              text="Sem instalar nada: tudo acontece no navegador, no computador ou no celular."
            />

            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              {steps.map((step) => (
                <div key={step.title} className={`${cardClass} flex flex-col`}>
                  <div
                    className={`mb-4 flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${step.iconBox}`}
                  >
                    <step.icon size={26} aria-hidden="true" />
                  </div>
                  <h3 className="mb-1 text-title-md font-bold text-on-surface">{step.title}</h3>
                  <p className="text-body-sm text-on-surface-variant">{step.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* PLANOS */}
        <PlansSection cardClass={cardClass} />

        {/* CTA FINAL */}
        <section className="w-full bg-surface py-12">
          <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
            <div className="relative overflow-hidden rounded-3xl bg-linear-to-r from-on-primary-fixed-variant via-primary to-secondary p-8 text-on-primary shadow-xl sm:p-12">
              <div className="pointer-events-none absolute -top-20 -right-20 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
              <div className="pointer-events-none absolute -bottom-20 -left-20 h-96 w-96 rounded-full bg-secondary-container/20 blur-3xl" />

              <div className="relative z-10 mx-auto flex max-w-3xl flex-col items-center gap-4 text-center">
                <div className="mb-1 flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 shadow-inner backdrop-blur-md">
                  <Rocket size={32} className="text-white" aria-hidden="true" />
                </div>

                <h2 className="text-display-lg-mobile text-white md:text-display-lg">
                  Pronto para abrir sua loja online?
                </h2>

                <p className="max-w-xl text-body-lg text-white/90">
                  Comece no plano grátis, monte a vitrine e mude de plano quando a loja crescer.
                </p>

                <div className="flex w-full flex-col items-center gap-4 pt-2 sm:w-auto sm:flex-row">
                  <Link
                    href="/auth/register"
                    className="inline-flex h-14 w-full items-center justify-center gap-2 rounded-xl bg-white px-8 text-title-md font-bold text-primary shadow-lg transition-all duration-200 hover:scale-105 hover:bg-surface-bright sm:w-auto"
                  >
                    Criar minha loja
                    <ArrowRight size={20} aria-hidden="true" />
                  </Link>
                </div>

                <div className="flex flex-wrap items-center justify-center gap-6 pt-1 text-label-sm text-white/80">
                  {ctaPerks.map((perk) => (
                    <span key={perk} className="inline-flex items-center gap-1">
                      <CircleCheck size={16} aria-hidden="true" />
                      {perk}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>

      {/* FOOTER */}
      <footer
        id="contato"
        className="w-full scroll-mt-20 bg-surface-container-lowest shadow-[0_-1px_8px_rgba(11,28,48,0.03)]"
      >
        <div className="mx-auto max-w-[1600px] px-4 pt-12 pb-8 sm:px-8">
          <div className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-5">
            <div className="flex flex-col gap-4 lg:col-span-2 lg:pr-8">
              <Logo />
              <p className="max-w-sm text-body-md text-on-surface-variant">
                Vitrine online, pedidos, estoque e pagamentos para pequenos lojistas, em um só lugar.
              </p>
            </div>

            {footerColumns.map((column) => (
              <div key={column.title} className="flex flex-col gap-2">
                <h4 className="mb-1 text-label-md font-semibold tracking-wider text-on-surface uppercase">
                  {column.title}
                </h4>
                <ul className="flex flex-col gap-2 text-body-sm text-on-surface-variant">
                  {column.links.map((link) => (
                    <li key={link.href}>
                      <Link href={link.href} className="transition-colors hover:text-primary">
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ))}

            <div className="flex flex-col gap-2">
              <h4 className="mb-1 text-label-md font-semibold tracking-wider text-on-surface uppercase">
                Contato & Suporte
              </h4>
              <ul className="flex flex-col gap-2 text-body-sm text-on-surface-variant">
                {SUPPORT_EMAIL && (
                  <li>
                    <a
                      href={`mailto:${SUPPORT_EMAIL}`}
                      className="font-mono text-code-sm transition-colors hover:text-primary"
                    >
                      {SUPPORT_EMAIL}
                    </a>
                  </li>
                )}
                <li>Feito para lojistas do Brasil</li>
              </ul>
            </div>
          </div>

          <div className="flex flex-col items-center justify-between gap-4 rounded-xl bg-surface-container-low/50 px-6 py-4 text-body-sm text-on-surface-variant md:flex-row">
            <p>© {new Date().getFullYear()} Vitrio System. Todos os direitos reservados. Brasil.</p>
          </div>
        </div>
      </footer>
    </div>
  );
}
