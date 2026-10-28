// Envia uma imagem para /api/upload (rota do próprio Next, que valida
// a sessão no backend e repassa pro Cloudinary). Retorna a URL pública.
// (Movido de app/api/upload/upload.ts: arquivos auxiliares não devem
// morar dentro de app/api, que é reservado pra rotas.)
import { getAccessToken } from "./api";

/** Limite do arquivo enviado à rota (o mesmo de app/api/upload/route.ts). Abaixo dos 4,5 MB da Vercel. */
export const MAX_UPLOAD_SIZE = 4 * 1024 * 1024;

/** Limite da foto escolhida, antes de reduzir no navegador (foto de celular passa fácil de 5 MB). */
export const MAX_SOURCE_SIZE = 25 * 1024 * 1024;

// Maior lado da imagem enviada. A vitrine mostra no máximo ~1200 px (lib/image.ts); o resto é
// peso à toa no upload e no armazenamento.
const MAX_DIMENSION = 2000;
// Até este tamanho, e dentro de MAX_DIMENSION, a imagem vai como está.
const KEEP_ORIGINAL_BELOW = 1024 * 1024;

const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];

export async function uploadImage(file: File): Promise<string> {
  const token = getAccessToken();
  if (!token) throw new Error("Sessão expirada. Faça login novamente.");

  if (!ALLOWED_TYPES.includes(file.type)) {
    throw new Error("Formato de imagem inválido. Use JPG, PNG ou WEBP.");
  }
  if (file.size > MAX_SOURCE_SIZE) {
    throw new Error("Imagem muito grande (máx. 25 MB).");
  }

  const upload = await shrinkImage(file);
  if (upload.size > MAX_UPLOAD_SIZE) {
    throw new Error("Não foi possível reduzir a imagem para menos de 4 MB. Tente outra foto.");
  }

  const formData = new FormData();
  formData.append("file", upload);

  const res = await fetch("/api/upload", {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: formData,
  });

  const data = await res.json().catch(() => null);

  if (!res.ok || !data?.status) {
    throw new Error(data?.mensagem ?? "Erro ao enviar a imagem.");
  }

  return data.dados as string;
}

// Reduz para no máximo MAX_DIMENSION px e regrava em WebP (mantém a transparência de logos em PNG).
// O navegador que não grava WebP (Safari) devolve PNG: aí uma foto vai em JPEG. Se o resultado não
// ficar menor que o original, ou se o navegador não conseguir ler a imagem, vai o original.
async function shrinkImage(file: File): Promise<File> {
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    return file;
  }

  try {
    const scale = Math.min(1, MAX_DIMENSION / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.size <= KEEP_ORIGINAL_BELOW) return file;

    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    const context = canvas.getContext("2d");
    if (!context) return file;
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);

    let blob = await toBlob(canvas, "image/webp", 0.85);
    if (blob && blob.type !== "image/webp" && file.type !== "image/png") {
      blob = await toBlob(canvas, "image/jpeg", 0.85);
    }
    if (!blob || !ALLOWED_TYPES.includes(blob.type) || blob.size >= file.size) return file;

    const extension = blob.type.split("/")[1].replace("jpeg", "jpg");
    const name = file.name.replace(/\.[^.]*$/, "") + "." + extension;
    return new File([blob], name, { type: blob.type });
  } finally {
    bitmap.close();
  }
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality: number) {
  return new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, quality));
}
