// Dados do Vitrio que aparecem em várias telas.

/**
 * E-mail de suporte (rodapé da página inicial e tela de Suporte do painel). Vem de
 * NEXT_PUBLIC_SUPPORT_EMAIL, fixada no build. Sem ela, as telas não mostram e-mail nenhum, para não
 * divulgar um endereço que ninguém lê.
 */
export const SUPPORT_EMAIL = process.env.NEXT_PUBLIC_SUPPORT_EMAIL?.trim() || null;
