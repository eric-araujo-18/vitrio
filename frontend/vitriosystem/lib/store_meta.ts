// Dados da loja lidos no servidor do Next, para a prévia do link (WhatsApp, Instagram...) e o título
// da aba. A vitrine em si é montada no navegador; quem compartilha o link nunca a vê. Só a página
// (o HTML) e a imagem de prévia usam isto. Só funciona no servidor (usa next/headers).
import { headers } from "next/headers";
import type { PublicStore } from "./api_public";

// URL do backend vista pelo servidor do Next (a mesma de app/api/upload/route.ts).
const API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5020";

// Uma loja muda de nome ou logo raramente: a prévia pode ficar até 5 minutos desatualizada.
const REVALIDATE_SECONDS = 300;

export async function fetchStoreMeta(slug: string): Promise<PublicStore | null> {
  try {
    const res = await fetch(`${API_URL}/api/Public/stores/${encodeURIComponent(slug)}`, {
      next: { revalidate: REVALIDATE_SECONDS },
    });
    if (!res.ok) return null;
    const data = await res.json();
    return data?.status && data.dados ? (data.dados as PublicStore) : null;
  } catch {
    return null;
  }
}

// Endereço público do site, para a imagem de prévia ter URL completa. SITE_URL quando definida;
// senão, o endereço pelo qual a página foi pedida (o proxy da hospedagem repassa o host original).
export async function siteUrl(): Promise<URL> {
  const configured = process.env.SITE_URL?.trim();
  if (configured) return new URL(configured);

  const h = await headers();
  const host = h.get("x-forwarded-host")?.split(",")[0].trim() || h.get("host") || "localhost:3000";
  const proto =
    h.get("x-forwarded-proto")?.split(",")[0].trim() || (/^(localhost|127\.0\.0\.1)(:|$)/.test(host) ? "http" : "https");
  return new URL(`${proto}://${host}`);
}
