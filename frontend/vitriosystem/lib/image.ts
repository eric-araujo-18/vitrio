// Imagens do Cloudinary no tamanho em que aparecem. A URL salva é a do arquivo original (fotos de
// celular com vários MB); inserindo transformações depois de "/image/upload/", o Cloudinary entrega
// uma cópia menor e em WebP/AVIF quando o navegador aceita:
//   f_auto: melhor formato para o navegador; q_auto: qualidade ajustada à imagem;
//   w_N,c_limit: no máximo N px de largura, sem aumentar imagens menores.
// Cada tamanho é gerado uma vez e fica em cache no Cloudinary (conta pouco no plano gratuito).
// Outras URLs (prévia local, imagem de outro site) voltam sem mudança.

const CLOUDINARY_UPLOAD = /^(https:\/\/res\.cloudinary\.com\/[^/]+\/image\/upload\/)(.+)$/;

/** Larguras usadas no site: múltiplos da largura na tela, para telas de alta densidade. */
export const IMAGE_WIDTH = {
  /** Miniaturas (carrinho, pedidos, logo no cabeçalho) */
  thumb: 160,
  /** Cards de produto da vitrine e do painel */
  card: 600,
  /** Foto aberta no produto */
  full: 1200,
} as const;

export function imageUrl(url: string, width: number): string;
export function imageUrl(url: string | null | undefined, width: number): string | undefined;
export function imageUrl(url: string | null | undefined, width: number) {
  if (!url) return url ?? undefined;
  const match = CLOUDINARY_UPLOAD.exec(url);
  if (!match) return url;
  return `${match[1]}f_auto,q_auto,w_${width},c_limit/${match[2]}`;
}

/**
 * Imagem para a prévia do link (WhatsApp, Instagram, Facebook): JPG quadrado com fundo branco.
 * Esses leitores nem sempre entendem WebP/AVIF, e imagens grandes demais são ignoradas.
 */
export function previewImageUrl(url: string | null | undefined): string | undefined {
  if (!url) return undefined;
  const match = CLOUDINARY_UPLOAD.exec(url);
  if (!match) return url;
  return `${match[1]}c_pad,w_600,h_600,b_white,f_jpg,q_auto/${match[2]}`;
}
