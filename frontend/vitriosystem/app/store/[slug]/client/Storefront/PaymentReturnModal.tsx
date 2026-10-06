"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { CircleAlert, CircleCheck, CreditCard, LoaderCircle, MessageCircle, X } from "lucide-react";
import { getOrderPayment, type OrderPaymentInfo } from "@/lib/api_public";
import { formatPrice, formatTime, whatsappLink } from "@/lib/format";
import { iconButton, storeOverlay, storePrimaryButton, storeSecondaryButton, useLockBodyScroll } from "./Ui";

// Confere a cada 4s nos primeiros 2 minutos (o pagamento costuma ser confirmado logo) e
// depois a cada 15s, até o pedido sair de "aguardando pagamento".
const FAST_POLL_MS = 4000;
const FAST_POLLS = 30;
const SLOW_POLL_MS = 15000;

interface PaymentReturnModalProps {
  slug: string;
  storeName: string;
  /** Chamado quando o pagamento termina (pago ou não): a vitrine recarrega o estoque. */
  onSettled?: () => void;
}

// Volta do checkout do Mercado Pago: a API redireciona para a vitrine com ?pedido=CODIGO.
// Usa useSearchParams: fica dentro de um <Suspense>.
export default function PaymentReturnModal(props: PaymentReturnModalProps) {
  const code = useSearchParams().get("pedido");
  const router = useRouter();
  const pathname = usePathname();
  if (!code) return null;
  // key: um pedido novo na URL começa do zero
  return <PaymentReturnDialog key={code} code={code} onClose={() => router.replace(pathname, { scroll: false })} {...props} />;
}

function PaymentReturnDialog({
  slug,
  storeName,
  code,
  onClose,
  onSettled,
}: PaymentReturnModalProps & { code: string; onClose: () => void }) {
  const [info, setInfo] = useState<OrderPaymentInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  useLockBodyScroll();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    let polls = 0;

    const tick = async () => {
      polls += 1;
      try {
        const res = await getOrderPayment(slug, code);
        if (stopped) return;
        if (!res.status || !res.dados) {
          setError(res.mensagem ?? "Pedido não encontrado.");
          return;
        }
        setInfo(res.dados);
        if (res.dados.status !== "AwaitingPayment") {
          onSettled?.();
          return;
        }
      } catch {
        if (stopped) return; // sem rede: tenta de novo na próxima
      }
      timer = setTimeout(tick, polls < FAST_POLLS ? FAST_POLL_MS : SLOW_POLL_MS);
    };

    void tick();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [slug, code, onSettled]);

  const paid = info?.paymentStatus === "Approved";
  const waiting = info?.status === "AwaitingPayment";

  return (
    <div className={`${storeOverlay} flex items-center justify-center p-4`} onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="payment-return-title"
        onClick={(e) => e.stopPropagation()}
        className="relative w-full max-w-[420px] rounded-2xl bg-white p-6 text-center shadow-[0_20px_60px_rgba(0,0,0,0.2)]"
      >
        <button type="button" onClick={onClose} aria-label="Fechar" className={`${iconButton} absolute top-3 right-3`}>
          <X size={20} aria-hidden="true" />
        </button>

        <div className="flex flex-col items-center gap-3">
          {error ? (
            <>
              <StatusIcon tone="error" />
              <h2 id="payment-return-title" className="text-headline-sm text-slate-900">
                Não encontramos o pedido
              </h2>
              <p className="text-body-md text-slate-500">{error}</p>
            </>
          ) : !info || waiting ? (
            <>
              <div className="mb-1 flex h-16 w-16 items-center justify-center rounded-full bg-[color-mix(in_srgb,var(--store-primary)_10%,white)] text-[var(--store-primary)]">
                <LoaderCircle size={32} aria-hidden="true" className="animate-spin" />
              </div>
              <h2 id="payment-return-title" className="text-headline-sm text-slate-900">
                Confirmando seu pagamento
              </h2>
              <p className="text-body-md text-slate-500">
                Pedido <span className="font-mono font-semibold">#{code}</span>
                {info && <> · {formatPrice(info.total)}</>}. Assim que o Mercado Pago confirmar, o pedido vai para a{" "}
                {storeName}.
              </p>
              {info?.checkoutUrl && (
                <a href={info.checkoutUrl} className={`${storeSecondaryButton} mt-2`}>
                  <CreditCard size={18} aria-hidden="true" />
                  Ainda não paguei
                  {info.paymentDeadline && (
                    <span className="font-normal text-slate-500">(até {formatTime(info.paymentDeadline)})</span>
                  )}
                </a>
              )}
            </>
          ) : paid ? (
            <>
              <StatusIcon tone="success" />
              <h2 id="payment-return-title" className="text-headline-sm text-slate-900">
                Pagamento aprovado!
              </h2>
              <p className="text-body-md text-slate-500">
                O pedido <span className="font-mono font-semibold">#{info.code}</span> ({formatPrice(info.total)}) foi
                enviado para a {storeName}.
              </p>
              {info.storePhone && (
                <a
                  href={whatsappLink(info.storePhone, `Olá! Acabei de pagar o pedido #${info.code} na ${storeName}.`)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={`${storePrimaryButton} mt-2`}
                >
                  <MessageCircle size={18} aria-hidden="true" />
                  Avisar a loja no WhatsApp
                </a>
              )}
            </>
          ) : (
            <>
              <StatusIcon tone="error" />
              <h2 id="payment-return-title" className="text-headline-sm text-slate-900">
                {info.paymentStatus === "Refunded" ? "Pagamento estornado" : "Pagamento não concluído"}
              </h2>
              <p className="text-body-md text-slate-500">
                {info.paymentStatus === "Refunded"
                  ? `O pedido #${info.code} foi cancelado e o valor foi devolvido para você.`
                  : `O pedido #${info.code} foi cancelado sem pagamento (o prazo acabou ou a loja cancelou). Dá para fazer o pedido de novo.`}
              </p>
            </>
          )}

          <button type="button" onClick={onClose} className={`${storeSecondaryButton} mt-1`}>
            Continuar comprando
          </button>
        </div>
      </div>
    </div>
  );
}

function StatusIcon({ tone }: { tone: "success" | "error" }) {
  return (
    <div
      className={`mb-1 flex h-16 w-16 items-center justify-center rounded-full ${
        tone === "success" ? "bg-emerald-500/10 text-emerald-600" : "bg-red-500/10 text-red-600"
      }`}
    >
      {tone === "success" ? (
        <CircleCheck size={36} aria-hidden="true" />
      ) : (
        <CircleAlert size={36} aria-hidden="true" />
      )}
    </div>
  );
}
