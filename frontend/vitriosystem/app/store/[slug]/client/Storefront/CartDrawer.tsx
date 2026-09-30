"use client";

import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import {
  ArrowLeft,
  CircleAlert,
  CircleCheck,
  ImageOff,
  LoaderCircle,
  MessageCircle,
  Send,
  ShoppingBag,
  Trash2,
  X,
} from "lucide-react";
import { createPublicOrder, type OrderCreated, type PublicStore } from "@/lib/api_public";
import { unwrap } from "@/lib/api";
import { useCart } from "@/lib/cart";
import { formatPrice, whatsappLink } from "@/lib/format";
import { formatPhone, isValidPhone, isvalidEmail } from "@/lib/validators";
import {
  QuantityStepper,
  iconButton,
  storeInput,
  storeOverlay,
  storePrimaryButton,
  storeSecondaryButton,
  useLockBodyScroll,
} from "./Ui";

interface CartDrawerProps {
  store: PublicStore;
  onClose: () => void;
}

type Step = "cart" | "checkout" | "done";

const STEP_TITLES: Record<Step, string> = {
  cart: "Seu carrinho",
  checkout: "Finalizar pedido",
  done: "Pedido enviado",
};

export default function CartDrawer({ store, onClose }: CartDrawerProps) {
  const { items, totalPrice, setQuantity, removeItem, clear } = useCart();

  const [step, setStep] = useState<Step>("cart");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [notes, setNotes] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<OrderCreated | null>(null);

  useLockBodyScroll();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !sending && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, sending]);

  async function handleCheckout(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (name.trim().length < 2) return setError("Informe seu nome.");
    if (!isValidPhone(phone)) return setError("Informe um telefone válido com DDD.");
    if (email.trim() && !isvalidEmail(email.trim())) return setError("E-mail inválido.");

    setSending(true);
    try {
      const result = await unwrap(
        createPublicOrder(store.slug, {
          customerName: name.trim(),
          customerPhone: phone,
          customerEmail: email.trim() || undefined,
          notes: notes.trim() || undefined,
          items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
        })
      );
      setCreated(result);
      clear();
      setStep("done");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível enviar o pedido.");
    } finally {
      setSending(false);
    }
  }

  const totalCount = items.reduce((sum, i) => sum + i.quantity, 0);

  return (
    <div className={`${storeOverlay} flex justify-end`} onClick={() => !sending && onClose()}>
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="cart-drawer-title"
        onClick={(e) => e.stopPropagation()}
        className="flex h-full w-full max-w-[420px] animate-drawer-in flex-col bg-white shadow-[-10px_0_30px_rgba(0,0,0,0.12)]"
      >
        {/* Cabeçalho */}
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div className="flex min-w-0 items-center gap-2">
            {step === "checkout" && (
              <button
                type="button"
                onClick={() => setStep("cart")}
                disabled={sending}
                aria-label="Voltar ao carrinho"
                className={`${iconButton} -ml-2`}
              >
                <ArrowLeft size={20} aria-hidden="true" />
              </button>
            )}
            <h2 id="cart-drawer-title" className="truncate text-headline-sm text-slate-900">
              {STEP_TITLES[step]}
              {step === "cart" && totalCount > 0 && (
                <span className="ml-1.5 font-normal text-slate-400">({totalCount})</span>
              )}
            </h2>
          </div>
          <button type="button" onClick={onClose} disabled={sending} aria-label="Fechar" className={iconButton}>
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        {step === "done" && created ? (
          /* Pedido enviado */
          <div className="flex flex-1 flex-col items-center justify-center gap-3 px-6 py-8 text-center">
            <div className="mb-2 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-600">
              <CircleCheck size={36} aria-hidden="true" />
            </div>
            <p className="text-headline-sm text-slate-900">
              Pedido <span className="font-mono">#{created.code}</span> recebido!
            </p>
            <p className="text-body-md text-slate-500">Total: {formatPrice(created.total)}</p>
            <p className="max-w-xs text-body-md text-slate-500">
              A loja vai entrar em contato pelo telefone informado para combinar pagamento e entrega.
            </p>

            <div className="mt-4 flex w-full flex-col gap-2.5">
              {created.storePhone && (
                <a
                  href={whatsappLink(
                    created.storePhone,
                    `Olá! Acabei de fazer o pedido #${created.code} na ${store.name}.`
                  )}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={storePrimaryButton}
                >
                  <MessageCircle size={18} aria-hidden="true" />
                  Avisar a loja no WhatsApp
                </a>
              )}
              <button type="button" onClick={onClose} className={storeSecondaryButton}>
                Continuar comprando
              </button>
            </div>
          </div>
        ) : items.length === 0 ? (
          /* Carrinho vazio */
          <div className="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center">
            <div className="flex h-16 w-16 items-center justify-center rounded-full bg-slate-100 text-slate-400">
              <ShoppingBag size={30} aria-hidden="true" />
            </div>
            <p className="text-body-lg text-slate-500">Seu carrinho está vazio.</p>
            <button type="button" onClick={onClose} className={`${storeSecondaryButton} mt-2 w-auto px-5`}>
              Ver produtos
            </button>
          </div>
        ) : (
          <>
            <div className="flex-1 overflow-y-auto px-5 py-2">
              {/* Itens */}
              {step === "cart" &&
                items.map((item) => (
                  <div key={item.productId} className="flex gap-3 border-b border-slate-100 py-4 last:border-b-0">
                    <div className="flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-slate-100 text-slate-400">
                      {item.imageUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img src={item.imageUrl} alt="" className="h-full w-full object-cover" />
                      ) : (
                        <ImageOff size={18} aria-hidden="true" />
                      )}
                    </div>

                    <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                      <span className="line-clamp-2 text-body-md font-semibold text-slate-900">{item.name}</span>
                      <span className="text-body-sm text-slate-500">{formatPrice(item.unitPrice)} cada</span>
                      <QuantityStepper
                        size="sm"
                        value={item.quantity}
                        onDecrease={() => setQuantity(item.productId, item.quantity - 1)}
                        onIncrease={() => setQuantity(item.productId, item.quantity + 1)}
                        canIncrease={item.quantity < item.maxQuantity}
                      />
                    </div>

                    <div className="flex flex-col items-end justify-between">
                      <strong className="text-body-md text-slate-900">
                        {formatPrice(item.unitPrice * item.quantity)}
                      </strong>
                      <button
                        type="button"
                        onClick={() => removeItem(item.productId)}
                        aria-label={`Remover ${item.name}`}
                        className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"
                      >
                        <Trash2 size={16} aria-hidden="true" />
                      </button>
                    </div>
                  </div>
                ))}

              {/* Dados do cliente */}
              {step === "checkout" && (
                <form
                  id="checkout-form"
                  onSubmit={handleCheckout}
                  className="flex flex-col gap-4 py-3"
                  noValidate
                >
                  <Field id="c-name" label="Seu nome" required>
                    <input
                      id="c-name"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      maxLength={100}
                      autoComplete="name"
                      disabled={sending}
                      className={`${storeInput} h-11`}
                    />
                  </Field>

                  <Field id="c-phone" label="Telefone / WhatsApp" required>
                    <input
                      id="c-phone"
                      type="tel"
                      value={phone}
                      onChange={(e) => setPhone(formatPhone(e.target.value))}
                      placeholder="(00) 00000-0000"
                      inputMode="numeric"
                      autoComplete="tel"
                      maxLength={15}
                      disabled={sending}
                      className={`${storeInput} h-11`}
                    />
                  </Field>

                  <Field id="c-email" label="E-mail" hint="(opcional)">
                    <input
                      id="c-email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      autoComplete="email"
                      disabled={sending}
                      className={`${storeInput} h-11`}
                    />
                  </Field>

                  <Field id="c-notes" label="Observações">
                    <textarea
                      id="c-notes"
                      rows={3}
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                      maxLength={500}
                      disabled={sending}
                      placeholder="Endereço de entrega, tamanho, cor, forma de pagamento..."
                      className={`${storeInput} resize-y py-2.5`}
                    />
                  </Field>
                </form>
              )}
            </div>

            {/* Rodapé */}
            <div className="flex flex-col gap-3 border-t border-slate-100 px-5 pt-4 pb-[calc(1.25rem+env(safe-area-inset-bottom))]">
              <div className="flex items-baseline justify-between">
                <span className="text-body-lg text-slate-600">Total</span>
                <strong className="text-[22px] font-extrabold text-slate-900">{formatPrice(totalPrice)}</strong>
              </div>

              {error && (
                <div
                  role="alert"
                  className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
                >
                  <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
                  <span>{error}</span>
                </div>
              )}

              {step === "cart" ? (
                <button type="button" onClick={() => setStep("checkout")} className={storePrimaryButton}>
                  Continuar
                </button>
              ) : (
                <>
                  <button type="submit" form="checkout-form" disabled={sending} className={storePrimaryButton}>
                    {sending ? (
                      <>
                        <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
                        Enviando...
                      </>
                    ) : (
                      <>
                        <Send size={18} aria-hidden="true" />
                        Enviar pedido
                      </>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => setStep("cart")}
                    disabled={sending}
                    className={storeSecondaryButton}
                  >
                    Voltar ao carrinho
                  </button>
                </>
              )}

              <p className="text-center text-body-sm text-slate-400">
                O pagamento é combinado diretamente com a loja.
              </p>
            </div>
          </>
        )}
      </aside>
    </div>
  );
}

function Field({
  id,
  label,
  hint,
  required,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  required?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-label-md font-semibold text-slate-700">
        {label}
        {required && <span className="ml-0.5 text-red-600">*</span>}
        {hint && <span className="ml-1 font-normal text-slate-400">{hint}</span>}
      </label>
      {children}
    </div>
  );
}