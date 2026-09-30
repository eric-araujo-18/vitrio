"use client";

import Link from "next/link";
import {
  ArrowLeftRight,
  ArrowRight,
  BadgeCheck,
  Check,
  CircleCheck,
  CreditCard,
  Handshake,
  Headphones,
  Package,
  QrCode,
  Receipt,
  Rocket,
  ShieldCheck,
  ShoppingCart,
  Store,
  TrendingUp,
  Zap,
} from "lucide-react";
import { useGuestOnly } from "@/lib/auth_context";

/* ===========================
   DADOS DA PÁGINA
   Deixar o conteúdo em arrays facilita editar
   textos sem mexer no layout.
=========================== */

const navLinks = [
  { label: "Recursos", href: "#recursos" },
  { label: "Diferenciais", href: "#diferenciais" },
  { label: "Contato", href: "#contato" },
];

const heroPerks = [
  { icon: BadgeCheck, text: "14 dias de teste grátis" },
  { icon: CreditCard, text: "Não pede cartão no cadastro" },
  { icon: Zap, text: "Configuração em menos de 5 min" },
];

const integrations = [
  { icon: QrCode, label: "Pix Instantâneo", color: "text-emerald-600" },
  { icon: Handshake, label: "Mercado Pago", color: "text-sky-600" },
  { icon: CreditCard, label: "Stripe Checkout", color: "text-primary" },
  { icon: Store, label: "Nuvemshop", color: "text-indigo-600" },
  { icon: ShoppingCart, label: "Shopify", color: "text-emerald-700" },
  { icon: ArrowLeftRight, label: "Bling ERP", color: "text-secondary" },
];

const features = [
  {
    icon: Package,
    tag: "MÓDULO 01",
    iconBox: "bg-primary/10 text-primary",
    tagColor: "text-primary",
    title: "Gestão Inteligente de Produtos",
    text: "Cadastre SKUs com fotos em alta resolução, configure grades de variação (tamanhos, cores, voltagens) e defina alertas automáticos para nunca perder vendas por falta de estoque.",
    showOrderTracker: false,
  },
  {
    icon: Receipt,
    tag: "MÓDULO 02",
    iconBox: "bg-secondary/10 text-secondary",
    tagColor: "text-secondary",
    title: "Controle Total de Pedidos & Faturamento",
    text: "Acompanhe o funil completo: do pagamento aprovado à separação, emissão de notas fiscais (NF-e/NFC-e) e despacho com código de rastreio enviado ao cliente via WhatsApp.",
    showOrderTracker: true,
  },
  {
    icon: TrendingUp,
    tag: "MÓDULO 03",
    iconBox: "bg-secondary-fixed/50 text-secondary",
    tagColor: "text-secondary",
    title: "Relatórios & Insights Financeiros",
    text: "Tenha clareza sobre sua margem de contribuição, DRE simplificada, curva ABC de produtos e fluxo projetado de entradas. Exporte relatórios em PDF e planilhas em 1 clique.",
    showOrderTracker: false,
  },
];

const orderSteps = [
  { label: "Pago", status: "done" },
  { label: "Separado", status: "done" },
  { label: "NF Emitida", status: "current" },
  { label: "Entregue", status: "pending" },
] as const;

const pillars = [
  {
    icon: Headphones,
    iconBox: "bg-secondary-fixed/50 text-secondary",
    title: "Suporte Humano Dedicado",
    text: "Fale com especialistas reais direto pelo WhatsApp em menos de 2 minutos.",
  },
  {
    icon: ShieldCheck,
    iconBox: "bg-primary/10 text-primary",
    title: "Alta Disponibilidade & Segurança",
    text: "99.98% de estabilidade com servidores redundantes e criptografia ponta a ponta.",
  },
  {
    icon: Rocket,
    iconBox: "bg-secondary/10 text-secondary",
    title: "Setup Descomplicado",
    text: "Importe seus produtos e comece a operar no mesmo dia sem consultoria técnica.",
  },
];

const ctaPerks = [
  "Sem necessidade de cartão de crédito",
  "Cancele quando quiser",
  "Suporte de migração incluído",
];

const footerColumns = [
  {
    title: "Produtos",
    links: [
      "ERP Integrado",
      "Controle de Estoque",
      "Faturamento & PDV",
      "CRM & Vendas",
      "Automação & IA",
    ],
  },
  {
    title: "Links Rápidos",
    links: [
      "Soluções Setoriais",
      "Planos & Preços",
      "Sobre a Empresa",
      "Termos de Serviço",
      "Política de Privacidade",
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
                Controle produtos, estoque, pedidos, clientes e vendas em um único lugar. Tudo em
                um sistema moderno, rápido, intuitivo e com segurança corporativa.
              </p>

              <div className="flex w-full flex-col items-center gap-4 sm:w-auto sm:flex-row">
                <Link
                  href="/auth/register"
                  className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-primary px-8 text-title-md text-on-primary shadow-[0_2px_4px_rgba(0,74,198,0.2),0_8px_20px_rgba(37,99,235,0.3)] transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary-container sm:w-auto"
                >
                  Começar Agora
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

        {/* INTEGRAÇÕES */}
        <section className="w-full bg-surface-container-lowest py-8 shadow-sm">
          <div className="mx-auto flex max-w-[1600px] flex-col items-center justify-center gap-4 px-4 text-center sm:px-8">
            <p className="text-label-md font-semibold tracking-wider text-outline uppercase">
              Integrado com os principais meios de pagamento e plataformas do Brasil
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
          </div>
        </section>

        {/* RECURSOS */}
        <section id="recursos" className="w-full scroll-mt-20 bg-surface py-12">
          <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
            <SectionHeading
              eyebrow="Arquitetura Unificada"
              eyebrowClass="rounded-full bg-primary-fixed/40 px-3 py-1 text-on-primary-fixed-variant"
              title="Tudo que sua loja precisa em um ecossistema rápido"
              text="Abandone planilhas confusas e sistemas desconectados. O Vitrio reúne cada elo do seu negócio com extrema clareza operacional."
            />

            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
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

        {/* DIFERENCIAIS */}
        <section id="diferenciais" className="w-full scroll-mt-20 bg-surface-container-low/40 py-12">
          <div className="mx-auto max-w-[1600px] px-4 sm:px-8">
            <SectionHeading
              eyebrow="Diferencial Competitivo"
              eyebrowClass="text-secondary"
              title="Por que as lojas líderes confiam no Vitrio System?"
              text="Construído para velocidade ininterrupta e estabilidade à prova de picos de tráfego, da Black Friday às operações diárias mais exigentes."
            />

            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              {pillars.map((pillar) => (
                <div key={pillar.title} className={`${cardClass} flex flex-col`}>
                  <div
                    className={`mb-4 flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${pillar.iconBox}`}
                  >
                    <pillar.icon size={26} aria-hidden="true" />
                  </div>
                  <h3 className="mb-1 text-title-md font-bold text-on-surface">{pillar.title}</h3>
                  <p className="text-body-sm text-on-surface-variant">{pillar.text}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

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
                  Pronto para transformar a gestão da sua loja?
                </h2>

                <p className="max-w-xl text-body-lg text-white/90">
                  Junte-se a mais de 2.500 lojistas em todo o Brasil. Comece agora mesmo seu
                  período de teste completo sem compromisso.
                </p>

                <div className="flex w-full flex-col items-center gap-4 pt-2 sm:w-auto sm:flex-row">
                  <Link
                    href="/auth/register"
                    className="inline-flex h-14 w-full items-center justify-center gap-2 rounded-xl bg-white px-8 text-title-md font-bold text-primary shadow-lg transition-all duration-200 hover:scale-105 hover:bg-surface-bright sm:w-auto"
                  >
                    Vamos Começar
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
                Plataforma completa para gestão de pequenas e médias empresas. Controle financeiro,
                estoques omnichannel, CRM e automação em um único ecossistema integrado.
              </p>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-container px-2.5 py-1 text-label-sm text-secondary">
                  <span className="h-2 w-2 rounded-full bg-secondary" />
                  ISO 27001 Certified
                </span>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-container px-2.5 py-1 text-label-sm text-primary">
                  <span className="h-2 w-2 rounded-full bg-primary" />
                  LGPD Compliant
                </span>
              </div>
            </div>

            {footerColumns.map((column) => (
              <div key={column.title} className="flex flex-col gap-2">
                <h4 className="mb-1 text-label-md font-semibold tracking-wider text-on-surface uppercase">
                  {column.title}
                </h4>
                <ul className="flex flex-col gap-2 text-body-sm text-on-surface-variant">
                  {column.links.map((label) => (
                    <li key={label}>
                      {/* TODO: apontar para as páginas reais quando existirem */}
                      <a href="#" className="transition-colors hover:text-primary">
                        {label}
                      </a>
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
                <li>
                  <a
                    href="mailto:contato@vitrio.com"
                    className="font-mono text-code-sm transition-colors hover:text-primary"
                  >
                    contato@vitrio.com
                  </a>
                </li>
                <li>Atendimento nacional • Brasil</li>
                <li>Segunda a Sexta, 08h às 19h</li>
                <li>
                  <a
                    href="#"
                    className="inline-flex items-center gap-1 pt-1 text-label-md text-primary hover:underline"
                  >
                    Central de Ajuda
                  </a>
                </li>
              </ul>
            </div>
          </div>

          <div className="flex flex-col items-center justify-between gap-4 rounded-xl bg-surface-container-low/50 px-6 py-4 text-body-sm text-on-surface-variant md:flex-row">
            <p>© {new Date().getFullYear()} Vitrio System. Todos os direitos reservados. Brasil.</p>
            <div className="flex flex-wrap items-center justify-center gap-6 text-label-sm">
              <a href="#" className="hover:text-on-surface">
                Segurança e Privacidade
              </a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}