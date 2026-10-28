"use client";

import { useEffect, useImperativeHandle, useRef, type Ref } from "react";

// Captcha da Cloudflare (Turnstile) no checkout. Na maioria das vezes ele se resolve sozinho e nem
// aparece (appearance "interaction-only"); só pede um clique quando desconfia. A resposta vale uma
// vez só: depois de cada tentativa de pedido, o checkout chama reset() para gerar outra.

interface TurnstileApi {
  render(container: HTMLElement, options: Record<string, unknown>): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

const SCRIPT_URL = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
let scriptPromise: Promise<void> | null = null;

function loadScript(): Promise<void> {
  if (window.turnstile) return Promise.resolve();
  scriptPromise ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = SCRIPT_URL;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => {
      scriptPromise = null; // deixa tentar de novo na próxima vez
      reject(new Error("Não foi possível carregar a verificação de segurança."));
    };
    document.head.appendChild(script);
  });
  return scriptPromise;
}

export interface TurnstileHandle {
  /** Descarta a resposta usada e gera outra. */
  reset(): void;
}

export default function Turnstile({
  siteKey,
  onToken,
  onLoadError,
  ref,
}: {
  siteKey: string;
  /** Resposta pronta para mandar com o pedido, ou null quando venceu ou foi descartada. */
  onToken: (token: string | null) => void;
  /** O script da Cloudflare não carregou (rede bloqueando, por exemplo). */
  onLoadError?: () => void;
  ref?: Ref<TurnstileHandle>;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const widgetId = useRef<string | null>(null);
  // Os callbacks mais recentes, sem recriar o widget a cada renderização.
  const callbacks = useRef({ onToken, onLoadError });
  useEffect(() => {
    callbacks.current = { onToken, onLoadError };
  });

  useEffect(() => {
    let cancelled = false;
    loadScript()
      .then(() => {
        if (cancelled || !containerRef.current || !window.turnstile) return;
        widgetId.current = window.turnstile.render(containerRef.current, {
          sitekey: siteKey,
          action: "checkout",
          language: "pt-br",
          appearance: "interaction-only",
          callback: (token: string) => callbacks.current.onToken(token),
          "expired-callback": () => callbacks.current.onToken(null),
          "error-callback": () => callbacks.current.onToken(null),
        });
      })
      .catch(() => !cancelled && callbacks.current.onLoadError?.());

    return () => {
      cancelled = true;
      if (widgetId.current && window.turnstile) window.turnstile.remove(widgetId.current);
      widgetId.current = null;
    };
  }, [siteKey]);

  useImperativeHandle(
    ref,
    () => ({
      reset() {
        callbacks.current.onToken(null);
        if (widgetId.current && window.turnstile) window.turnstile.reset(widgetId.current);
      },
    }),
    []
  );

  return <div ref={containerRef} className="flex justify-center empty:hidden" />;
}
