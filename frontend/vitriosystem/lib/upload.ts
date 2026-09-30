// Envia uma imagem para /api/upload (rota do próprio Next, que valida
// a sessão no backend e repassa pro Cloudinary). Retorna a URL pública.
// (Movido de app/api/upload/upload.ts: arquivos auxiliares não devem
// morar dentro de app/api, que é reservado pra rotas.)
import { getAccessToken } from "./api";

export const MAX_UPLOAD_SIZE = 5 * 1024 * 1024; // 5MB — mesmo limite da rota

export async function uploadImage(file: File): Promise<string> {
  const token = getAccessToken();
  if (!token) throw new Error("Sessão expirada. Faça login novamente.");

  if (file.size > MAX_UPLOAD_SIZE) {
    throw new Error("Imagem muito grande (máx. 5MB).");
  }

  const formData = new FormData();
  formData.append("file", file);

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
