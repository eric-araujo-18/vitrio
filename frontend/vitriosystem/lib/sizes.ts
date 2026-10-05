// ===== Grades de tamanho =====
// O backend guarda o tamanho como texto livre; as grades abaixo são só
// as opções que o formulário do lojista oferece.

export type SizeGrid = "none" | "clothing" | "numeric";

export const SIZE_GRIDS: Record<Exclude<SizeGrid, "none">, { label: string; sizes: string[] }> = {
  clothing: {
    label: "Roupa (P a XG)",
    sizes: ["P", "M", "G", "GG", "XG"],
  },
  numeric: {
    label: "Numeração (36 a 60)",
    sizes: ["36", "38", "40", "42", "44", "46", "48", "50", "52", "54", "56", "58", "60"],
  },
};

/** Descobre a grade de um produto já salvo a partir dos tamanhos dele. */
export function detectSizeGrid(sizes: string[]): SizeGrid {
  if (sizes.length === 0) return "none";
  const numeric = new Set(SIZE_GRIDS.numeric.sizes);
  return sizes.every((s) => numeric.has(s)) ? "numeric" : "clothing";
}