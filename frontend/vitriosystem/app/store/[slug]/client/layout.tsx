import type { Metadata } from "next";
import type { ReactNode } from "react";
import { fetchStoreMeta, siteUrl } from "@/lib/store_meta";

// A vitrine (page.tsx) é montada no navegador. Este layout roda no servidor só para o HTML já sair
// com o nome, a descrição e a imagem da loja: é isso que o WhatsApp, o Instagram e o Facebook
// mostram na prévia do link que o lojista divulga. A imagem vem de opengraph-image.tsx.
export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const store = await fetchStoreMeta(slug);

  if (!store) {
    return { title: "Loja não encontrada", robots: { index: false } };
  }

  const description = store.description?.trim() || `Veja os produtos da ${store.name} e faça seu pedido.`;

  return {
    metadataBase: await siteUrl(),
    title: { absolute: store.name },
    description,
    openGraph: {
      type: "website",
      siteName: "Vitrio",
      locale: "pt_BR",
      title: store.name,
      description,
    },
    twitter: { card: "summary_large_image", title: store.name, description },
  };
}

export default function StorefrontLayout({ children }: { children: ReactNode }) {
  return children;
}
