"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { CreditCard, LoaderCircle, Unplug, X } from "lucide-react";
import { unwrap } from "@/lib/api";
import {
  connectStorePayment,
  disconnectStorePayment,
  getStorePaymentStatus,
  type StorePaymentStatus,
} from "@/lib/api_store_payment";
import {
  ErrorBox,
  InlineConfirm,
  LoadingState,
  StatusBadge,
  SuccessBox,
  WarningBox,
  btnPrimary,
  btnSecondary,
  card,
  cardSubtitle,
  cardTitle,
} from "./Ui";

// Resultado da volta do Mercado Pago (?mercadopago=...), definido pela API em StorePaymentService.
const RETURN_NOTICES: Record<string, { kind: "success" | "warning" | "error"; text: string }> = {
  conectado: { kind: "success", text: "Conta do Mercado Pago conectada. A vitrine já oferece “pagar agora”." },
  cancelado: { kind: "warning", text: "A conexão foi cancelada no Mercado Pago. Nada mudou." },
  erro: { kind: "error", text: "Não foi possível conectar a conta do Mercado Pago. Tente de novo." },
};

// Cartão "Pagamento online" das configurações da loja. Usa useSearchParams: fica dentro de um <Suspense>.
export default function OnlinePaymentCard({ storeId }: { storeId: number }) {
  const router = useRouter();
  const pathname = usePathname();
  const returnNotice = RETURN_NOTICES[useSearchParams().get("mercadopago") ?? ""];

  // Chave da loja carregada: enquanto for diferente, está carregando (sem setState no efeito).
  const [loaded, setLoaded] = useState<{ storeId: number; status: StorePaymentStatus | null } | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState<"connect" | "disconnect" | null>(null);
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getStorePaymentStatus(storeId)
      .then((res) => {
        if (!active) return;
        if (res.status && res.dados) setLoaded({ storeId, status: res.dados });
        else {
          setLoaded({ storeId, status: null });
          setLoadError(res.mensagem ?? "Erro ao carregar o pagamento online.");
        }
      })
      .catch(() => {
        if (!active) return;
        setLoaded({ storeId, status: null });
        setLoadError("Erro ao carregar o pagamento online.");
      });
    return () => {
      active = false;
    };
  }, [storeId]);

  const status = loaded?.storeId === storeId ? loaded.status : undefined;

  function clearReturnNotice() {
    if (returnNotice) router.replace(pathname, { scroll: false });
  }

  async function handleConnect() {
    clearReturnNotice();
    setError(null);
    setNotice(null);
    setBusy("connect");
    try {
      const { authorizationUrl } = await unwrap(connectStorePayment(storeId));
      window.location.assign(authorizationUrl); // autoriza no Mercado Pago e volta para esta página
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível conectar.");
      setBusy(null);
    }
  }

  async function handleDisconnect() {
    clearReturnNotice();
    setError(null);
    setBusy("disconnect");
    try {
      const updated = await unwrap(disconnectStorePayment(storeId));
      setLoaded({ storeId, status: updated });
      setConfirmingDisconnect(false);
      setNotice("Conta desconectada. A vitrine volta a oferecer só “combinar com a loja”.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível desconectar.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <section className={card}>
      <div className="flex flex-wrap items-center gap-2">
        <h2 className={cardTitle}>Pagamento online</h2>
        {status?.connected && (
          <StatusBadge status={status.available ? "Active" : "Inactive"}>
            {status.available ? "Conectado" : "Conectado (indisponível no plano)"}
          </StatusBadge>
        )}
        {status?.connected && !status.liveMode && <StatusBadge status="Pending">Conta de teste</StatusBadge>}
      </div>
      <p className={cardSubtitle}>
        Com a sua conta do Mercado Pago conectada, o cliente pode pagar o pedido na hora com Pix ou cartão. O dinheiro
        cai direto na sua conta, e o pedido só chega para você depois de pago.
      </p>

      <div className="mt-4 flex flex-col gap-3">
        {returnNotice && (
          <div className="relative">
            {returnNotice.kind === "success" ? (
              <SuccessBox>{returnNotice.text}</SuccessBox>
            ) : returnNotice.kind === "warning" ? (
              <WarningBox>{returnNotice.text}</WarningBox>
            ) : (
              <ErrorBox>{returnNotice.text}</ErrorBox>
            )}
            <button
              type="button"
              onClick={clearReturnNotice}
              aria-label="Fechar aviso"
              className="absolute top-2 right-2 rounded p-0.5 text-current opacity-60 hover:opacity-100"
            >
              <X size={16} aria-hidden="true" />
            </button>
          </div>
        )}
        {notice && <SuccessBox>{notice}</SuccessBox>}
        {error && <ErrorBox>{error}</ErrorBox>}

        {status === undefined ? (
          <LoadingState />
        ) : status === null ? (
          <ErrorBox>{loadError}</ErrorBox>
        ) : !status.configured ? (
          <p className="text-body-md text-on-surface-variant">O pagamento online ainda não está disponível no Vitrio.</p>
        ) : !status.planAllows && !status.connected ? (
          <WarningBox>
            Disponível nos planos Essencial e Profissional.{" "}
            <Link href="/menu/subscription" className="font-semibold underline hover:no-underline">
              Ver os planos
            </Link>
          </WarningBox>
        ) : !status.connected ? (
          <div className="flex justify-start">
            <button type="button" onClick={handleConnect} disabled={busy !== null} className={btnPrimary}>
              {busy === "connect" ? (
                <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
              ) : (
                <CreditCard size={16} aria-hidden="true" />
              )}
              Conectar Mercado Pago
            </button>
          </div>
        ) : (
          <>
            {!status.planAllows && (
              <WarningBox>
                Seu plano atual não inclui pagamento online: a vitrine oferece só “combinar com a loja”.{" "}
                <Link href="/menu/subscription" className="font-semibold underline hover:no-underline">
                  Ver os planos
                </Link>
              </WarningBox>
            )}
            <p className="text-body-sm text-on-surface-variant">
              Para o <strong className="text-on-surface">Pix</strong> aparecer no pagamento, a sua conta do Mercado Pago
              precisa ter uma chave Pix cadastrada (no app do Mercado Pago: Seu perfil → Suas chaves Pix). Sem chave, o
              cliente paga só com cartão.
            </p>
            <p className="text-body-md text-on-surface-variant">
              Conta do Mercado Pago <strong className="font-mono text-on-surface">{status.mercadoPagoUserId}</strong>.
              {status.awaitingPaymentOrders > 0 &&
                ` ${status.awaitingPaymentOrders} ${status.awaitingPaymentOrders === 1 ? "pedido esperando" : "pedidos esperando"} pagamento agora.`}
            </p>

            {confirmingDisconnect ? (
              <InlineConfirm
                message="A vitrine deixa de oferecer “pagar agora”. Pedidos já pagos continuam pagos, mas cancelar um deles não estorna mais pelo Vitrio."
                confirmLabel="Desconectar"
                onConfirm={handleDisconnect}
                onCancel={() => setConfirmingDisconnect(false)}
                busy={busy === "disconnect"}
              />
            ) : (
              <div className="flex justify-start">
                <button
                  type="button"
                  onClick={() => setConfirmingDisconnect(true)}
                  disabled={busy !== null}
                  className={btnSecondary}
                >
                  <Unplug size={16} aria-hidden="true" />
                  Desconectar
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
}
