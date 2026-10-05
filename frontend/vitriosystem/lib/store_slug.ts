// Slug de loja vindo da URL (?store=...). Só aceita o formato que o backend gera,
// porque ele vai parar dentro de links da página.
export function safeStoreSlug(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" && /^[a-z0-9-]{1,100}$/.test(value) ? value : undefined;
}
