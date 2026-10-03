// ===== Endereço de entrega =====

export interface ShippingAddress {
  cep: string; // só dígitos
  state: string; // UF
  city: string;
  neighborhood: string | null;
  street: string;
  number: string;
  complement: string | null;
}

/** Endereço salvo na conta do cliente */
export interface SavedAddress extends ShippingAddress {
  id: number;
  label: string | null;
  isDefault: boolean;
}

/** Valores do formulário (tudo string, do jeito que o usuário digita) */
export interface AddressFormValues {
  label: string;
  cep: string;
  state: string;
  city: string;
  neighborhood: string;
  street: string;
  number: string;
  complement: string;
}

export const EMPTY_ADDRESS: AddressFormValues = {
  label: "",
  cep: "",
  state: "",
  city: "",
  neighborhood: "",
  street: "",
  number: "",
  complement: "",
};

export const UFS = [
  "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
  "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
];

/** 00000-000 enquanto digita */
export function formatCep(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 8);
  return digits.length > 5 ? `${digits.slice(0, 5)}-${digits.slice(5)}` : digits;
}

/** Mesmas regras do backend (AddressHelper). Retorna a mensagem de erro ou null. */
export function validateAddress(v: AddressFormValues): string | null {
  if (v.cep.replace(/\D/g, "").length !== 8) return "Informe um CEP válido.";
  if (!UFS.includes(v.state)) return "Selecione a UF.";
  if (v.city.trim().length < 2) return "Informe o município.";
  if (v.street.trim().length < 2) return "Informe o nome da rua.";
  if (!v.number.trim()) return "Informe o número (ou S/N se não tiver).";
  return null;
}

/** Converte o formulário no formato que a API espera */
export function toAddressPayload(v: AddressFormValues, isDefault = false) {
  return {
    label: v.label.trim() || undefined,
    cep: v.cep.replace(/\D/g, ""),
    state: v.state,
    city: v.city.trim(),
    neighborhood: v.neighborhood.trim() || undefined,
    street: v.street.trim(),
    number: v.number.trim(),
    complement: v.complement.trim() || undefined,
    isDefault,
  };
}

export function toFormValues(a: SavedAddress): AddressFormValues {
  return {
    label: a.label ?? "",
    cep: formatCep(a.cep),
    state: a.state,
    city: a.city,
    neighborhood: a.neighborhood ?? "",
    street: a.street,
    number: a.number,
    complement: a.complement ?? "",
  };
}

/** "Rua X, 123 - Apto 2" */
export function addressLine1(a: ShippingAddress) {
  return `${a.street}, ${a.number}${a.complement ? ` - ${a.complement}` : ""}`;
}

/** "Centro, Ubajara/CE - 62350-000" */
export function addressLine2(a: ShippingAddress) {
  return `${a.neighborhood ? `${a.neighborhood}, ` : ""}${a.city}/${a.state} - ${formatCep(a.cep)}`;
}

/**
 * Busca o endereço pelo CEP no ViaCEP (serviço público e gratuito).
 * Só preenche o formulário para agilizar: se falhar, o cliente digita normalmente.
 */
export async function lookupCep(cep: string): Promise<Partial<AddressFormValues> | null> {
  const digits = cep.replace(/\D/g, "");
  if (digits.length !== 8) return null;
  try {
    const res = await fetch(`https://viacep.com.br/ws/${digits}/json/`);
    if (!res.ok) return null;
    const data = await res.json();
    if (data.erro) return null;
    return {
      street: data.logradouro || "",
      neighborhood: data.bairro || "",
      city: data.localidade || "",
      state: data.uf || "",
    };
  } catch {
    return null;
  }
}