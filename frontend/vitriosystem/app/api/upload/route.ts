// app/api/upload/route.ts
import { v2 as cloudinary } from "cloudinary";
import { NextRequest, NextResponse } from "next/server";

cloudinary.config({
  cloud_name: process.env.CLOUDINARY_CLOUD_NAME,
  api_key: process.env.CLOUDINARY_API_KEY,
  api_secret: process.env.CLOUDINARY_API_SECRET,
});

// URL do backend vista pelo servidor do Next (pode ser diferente da pública em produção).
const API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5020";

// Pasta no Cloudinary. Precisa ser a mesma de Cloudinary:Folder no backend, que apaga as
// imagens sem uso dessa pasta. Se dev e produção usarem a mesma conta, use pastas diferentes.
const FOLDER = process.env.CLOUDINARY_FOLDER || "vitrio";

const MAX_SIZE = 5 * 1024 * 1024; // 5MB
const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];

function fail(mensagem: string, status: number) {
  return NextResponse.json({ dados: null, mensagem, status: false }, { status });
}

// Antes a rota só checava se o header começava com "Bearer " — qualquer um
// mandando "Bearer abc" conseguia subir arquivos na sua conta do Cloudinary.
// Agora o token é validado de verdade no backend (que conhece a chave do JWT).
async function isAuthenticated(authHeader: string): Promise<boolean> {
  try {
    const res = await fetch(`${API_URL}/api/Auth/me`, {
      headers: { Authorization: authHeader },
      cache: "no-store",
    });
    if (!res.ok) return false;
    const data = await res.json();
    return data?.status === true && ["Shopkeeper", "Admin"].includes(data?.dados?.role);
  } catch {
    return false;
  }
}

export async function POST(req: NextRequest) {
  try {
    // 1. Autenticação — antes de ler o arquivo
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ") || !(await isAuthenticated(authHeader))) {
      return fail("Não autenticado.", 401);
    }

    // 2. Barra arquivos grandes antes de carregar o corpo inteiro na memória
    const contentLength = Number(req.headers.get("content-length") ?? 0);
    if (contentLength > MAX_SIZE + 1024 * 64) {
      return fail("Imagem muito grande (máx. 5MB).", 413);
    }

    const formData = await req.formData();
    const file = formData.get("file");

    if (!(file instanceof File)) {
      return fail("Nenhum arquivo enviado.", 400);
    }

    // 3. Tipo de arquivo
    if (!ALLOWED_TYPES.includes(file.type)) {
      return fail("Formato de imagem inválido. Use JPG, PNG ou WEBP.", 400);
    }

    // 4. Tamanho
    if (file.size > MAX_SIZE) {
      return fail("Imagem muito grande (máx. 5MB).", 400);
    }

    // 5. Upload
    const buffer = Buffer.from(await file.arrayBuffer());
    const base64 = `data:${file.type};base64,${buffer.toString("base64")}`;

    const uploadResult = await cloudinary.uploader.upload(base64, {
      folder: FOLDER,
      resource_type: "image", // Cloudinary rejeita o que não for imagem de verdade
    });

    return NextResponse.json({
      dados: uploadResult.secure_url,
      mensagem: null,
      status: true,
    });
  } catch (error) {
    console.error(error);
    return fail("Erro ao enviar a imagem.", 500);
  }
}
