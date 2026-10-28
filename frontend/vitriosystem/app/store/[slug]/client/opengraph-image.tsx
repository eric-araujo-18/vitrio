import { ImageResponse } from "next/og";
import { previewImageUrl } from "@/lib/image";
import { fetchStoreMeta } from "@/lib/store_meta";

// Imagem da prévia do link da vitrine (WhatsApp, Instagram, Facebook): nome, descrição e logo da
// loja, nas cores dela. 1200x630 é o tamanho que essas redes mostram grande.
export const alt = "Vitrine da loja";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

const DESCRIPTION_MAX = 140;

export default async function Image({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const store = await fetchStoreMeta(slug);

  const name = store?.name ?? "Vitrio";
  const primary = store?.primaryColor || "#2563eb";
  const secondary = store?.secondaryColor || "#1d4ed8";
  const rawDescription = store?.description?.trim() || (store ? "Veja os produtos e faça seu pedido." : "Crie sua loja online.");
  const description =
    rawDescription.length > DESCRIPTION_MAX ? `${rawDescription.slice(0, DESCRIPTION_MAX - 1).trimEnd()}…` : rawDescription;
  // JPG quadrado do Cloudinary: o gerador da imagem não lê WebP.
  const logo = previewImageUrl(store?.logoUrl);

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          gap: 56,
          padding: "0 80px",
          color: "white",
          background: `linear-gradient(135deg, ${primary}, ${secondary})`,
        }}
      >
        <div
          style={{
            width: 240,
            height: 240,
            flexShrink: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            overflow: "hidden",
            borderRadius: 40,
            background: "white",
            color: primary,
            fontSize: 120,
          }}
        >
          {logo ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={logo} alt="" width={240} height={240} style={{ objectFit: "cover" }} />
          ) : (
            name.charAt(0).toUpperCase()
          )}
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 20, minWidth: 0 }}>
          <div style={{ fontSize: name.length > 28 ? 60 : 76, lineHeight: 1.1 }}>{name}</div>
          <div style={{ fontSize: 32, lineHeight: 1.35, opacity: 0.9 }}>{description}</div>
          <div style={{ marginTop: 12, fontSize: 26, opacity: 0.75 }}>Faça seu pedido pela vitrine</div>
        </div>
      </div>
    ),
    size
  );
}
