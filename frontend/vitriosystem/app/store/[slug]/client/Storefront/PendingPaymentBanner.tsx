"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { CircleCheck, CreditCard, LoaderCircle, ShoppingBag, X } from "lucide-react";
import { cancelUnpaidOrder, getOrderPayment, type OrderPaymentInfo } from "@/lib/api_public";
import { useCart } from "@/lib/cart";
import { formatPrice, formatTime } from "@/lib/format";
import { clearPendingPayment, usePendingPayment, type PendingPayment } from "@/lib/pending_payment";
import { iconButton } from "./Ui";

interface PendingPaymentBannerProps {
  slug: string;
  /** O estoque mudou (pedido cancelado): a vitrine recarrega os produtos. */
  onStockChanged: () => void;
}

// Pedido que o cliente foi pagar no Mercado Pago e ainda não pagou: "pagar agora" ou "cancelar".
// Cancelado (por ele ou porque o prazo acabou), os itens voltam para o carrinho.
// Usa useSearchParams: fica dentro de um <Suspense>.
export default function PendingPaymentBanner({ slug, onStockChanged }: PendingPaymentBannerProps) {
  const pending = usePendingPayment(slug);
  // Voltando do checkout com ?pedido=, quem mostra a situação é o PaymentReturnModal.
  const returning = useSearchParams().get("pedido");
  const [message, setMessage] = useState<{ tone: "success" | "info"; text: string } | null>(null);

  if (message) {
    return (
      <Notice tone={message.tone} onClose={() => setMessage(null)}>
        {message.text}
      </Notice>
    );
  }
  if (!pending || returning === pending.code) return null;

  return (
    <PendingPaymentCard
      key={pending.code}
      slug={slug}
      pending={pending}
      onStockChanged={onStockChanged}
      onResolved={(result) => {
        if (result) setMessage(result);
        clearPendingPayment(slug, pending.code);
      }}
    />
  );
}

function PendingPaymentCard({
  slug,
  pending,
  onStockChanged,
  onResolved,
}: {
  slug: string;
  pending: PendingPayment;
  onStockChanged: () => void;
  onResolved: (message: { tone: "success" | "info"; text: string } | null) => void;
}) {
  const { restoreItems } = useCart();
  const [info, setInfo] = useState<OrderPaymentInfo | null>(null);
  const [canceling, setCanceling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // O que fazer com a situação atual do pedido. Devolve true se ele ainda espera pagamento.
  function settle(current: OrderPaymentInfo): boolean {
    if (current.status === "AwaitingPayment") return true;

    const paid = current.paymentStatus === "Approved" && current.status !== "Canceled";
    if (paid) {
      onResolved({ tone: "success", text: `O pagamento do pedido #${current.code} foi aprovado. A loja já recebeu o pedido.` });
    } else if (current.status === "Canceled" && current.paymentStatus === "Canceled") {
      // Não foi pago: os itens voltam para o carrinho (e o estoque já voltou para a loja).
      restoreItems(pending.items);
      onStockChanged();
      onResolved({
        tone: "info",
        text: `O pedido #${current.code} foi cancelado sem pagamento. Os itens voltaram para o seu carrinho.`,
      });
    } else {
      onResolved(null);
    }
    return false;
  }

  useEffect(() => {
    let active = true;
    getOrderPayment(slug, pending.code)
      .then((res) => {
        if (!active) return;
        if (!res.status || !res.dados) {
          onResolved(null); // o pedido não existe mais
          return;
        }
        if (settle(res.dados)) setInfo(res.dados);
      })
      .catch(() => {
        // sem rede: tenta de novo na próxima visita
      });
    return () => {
      active = false;
    };
    // Confere uma vez por pedido (o componente é recriado a cada pedido novo, pela key).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slug, pending.code]);

  async function handleCancel() {
    setCanceling(true);
    setError(null);
    try {
      const res = await cancelUnpaidOrder(slug, pending.code);
      if (!res.status || !res.dados) throw new Error(res.mensagem ?? "Não foi possível cancelar agora.");
      if (settle(res.dados)) setInfo(res.dados);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível cancelar agora.");
    } finally {
      setCanceling(false);
    }
  }

  if (!info) return null;

  return (
    <section
      aria-label="Pedido aguardando pagamento"
      className="mb-7 flex flex-col gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5"
    >
      <div className="flex min-w-0 items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-amber-100 text-amber-700">
          <CreditCard size={20} aria-hidden="true" />
        </div>
        <div className="min-w-0">
          <p className="text-body-lg font-semibold text-slate-900">
            Seu pedido <span className="font-mono">#{info.code}</span> está esperando o pagamento
          </p>
          <p className="text-body-md text-slate-600">
            {formatPrice(info.total)} ·{" "}
            {info.checkoutUrl && info.paymentDeadline
              ? `dá para pagar até ${formatTime(info.paymentDeadline)}. Se não quiser mais, cancele: os itens voltam para o carrinho.`
              : "o prazo para pagar acabou. O pedido será cancelado em alguns minutos e os itens voltam para o carrinho."}
          </p>
          {error && <p className="mt-1 text-body-md font-semibold text-red-700">{error}</p>}
        </div>
      </div>

      <div className="flex shrink-0 flex-col gap-2 sm:flex-row">
        <button
          type="button"
          onClick={handleCancel}
          disabled={canceling}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-title-md text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {canceling ? <LoaderCircle size={16} aria-hidden="true" className="animate-spin" /> : <ShoppingBag size={16} aria-hidden="true" />}
          Cancelar pedido
        </button>
        {info.checkoutUrl && (
          <a
            href={info.checkoutUrl}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--store-primary)] px-4 text-title-md font-bold text-white transition-colors hover:bg-[var(--store-secondary)]"
          >
            <CreditCard size={16} aria-hidden="true" />
            Pagar agora
          </a>
        )}
      </div>
    </section>
  );
}

function Notice({ tone, onClose, children }: { tone: "success" | "info"; onClose: () => void; children: string }) {
  return (
    <div
      role="status"
      className={`mb-7 flex items-start gap-3 rounded-2xl border p-4 ${
        tone === "success" ? "border-emerald-200 bg-emerald-50 text-emerald-800" : "border-slate-200 bg-white text-slate-700"
      }`}
    >
      {tone === "success" ? (
        <CircleCheck size={20} aria-hidden="true" className="mt-0.5 shrink-0" />
      ) : (
        <ShoppingBag size={20} aria-hidden="true" className="mt-0.5 shrink-0" />
      )}
      <p className="flex-1 text-body-md">{children}</p>
      <button type="button" onClick={onClose} aria-label="Fechar aviso" className={`${iconButton} -m-1.5 h-8 w-8`}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
