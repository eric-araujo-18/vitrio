"use client";

import { useState, type ReactNode } from "react";
import { LoaderCircle } from "lucide-react";
import { UFS, formatCep, lookupCep, type AddressFormValues } from "@/lib/address";
import { storeInput } from "./Ui";

/*
  Campos de endereço reaproveitados no checkout (cliente sem login ou
  "novo endereço") e em "Meus endereços".
  Ao completar o CEP, busca rua, bairro, município e UF no ViaCEP —
  o cliente só confere e digita o número.
*/

interface AddressFieldsProps {
  idPrefix: string;
  value: AddressFormValues;
  onChange: (value: AddressFormValues) => void;
  disabled?: boolean;
  /** Mostra o campo "Apelido" (Casa, Trabalho...) — só faz sentido para endereço salvo */
  showLabel?: boolean;
}

export default function AddressFields({ idPrefix, value, onChange, disabled, showLabel }: AddressFieldsProps) {
  const [lookingUp, setLookingUp] = useState(false);
  const [cepNotFound, setCepNotFound] = useState(false);

  const id = (name: string) => `${idPrefix}-${name}`;
  const set = (field: keyof AddressFormValues, fieldValue: string) => onChange({ ...value, [field]: fieldValue });
  const input = `${storeInput} h-11`;

  async function handleCep(raw: string) {
    const cep = formatCep(raw);
    const next = { ...value, cep };
    onChange(next);
    setCepNotFound(false);

    if (cep.replace(/\D/g, "").length !== 8) return;

    setLookingUp(true);
    const found = await lookupCep(cep);
    setLookingUp(false);

    if (!found) {
      setCepNotFound(true);
      return;
    }
    // Só preenche o que veio; o que o ViaCEP não tiver (ex: CEP geral da cidade) fica para o cliente.
    onChange({
      ...next,
      street: found.street || next.street,
      neighborhood: found.neighborhood || next.neighborhood,
      city: found.city || next.city,
      state: found.state || next.state,
    });
  }

  return (
    <div className="grid grid-cols-6 gap-3">
      {showLabel && (
        <Field id={id("label")} label="Apelido" hint="(opcional)" className="col-span-6">
          <input
            id={id("label")}
            value={value.label}
            onChange={(e) => set("label", e.target.value)}
            placeholder="Ex: Casa, Trabalho"
            maxLength={40}
            disabled={disabled}
            className={input}
          />
        </Field>
      )}

      <Field id={id("cep")} label="CEP" required className="col-span-3">
        <div className="relative">
          <input
            id={id("cep")}
            value={value.cep}
            onChange={(e) => handleCep(e.target.value)}
            placeholder="00000-000"
            inputMode="numeric"
            autoComplete="postal-code"
            maxLength={9}
            disabled={disabled}
            className={`${input} pr-9 tabular-nums`}
          />
          {lookingUp && (
            <LoaderCircle
              size={16}
              aria-label="Buscando CEP"
              className="absolute top-1/2 right-3 -translate-y-1/2 animate-spin text-slate-400"
            />
          )}
        </div>
      </Field>

      <Field id={id("state")} label="UF" required className="col-span-3">
        <select
          id={id("state")}
          value={value.state}
          onChange={(e) => set("state", e.target.value)}
          autoComplete="address-level1"
          disabled={disabled}
          className={input}
        >
          <option value="">Selecione</option>
          {UFS.map((uf) => (
            <option key={uf} value={uf}>
              {uf}
            </option>
          ))}
        </select>
      </Field>

      {cepNotFound && (
        <p className="col-span-6 -mt-1 text-body-sm text-amber-700">
          Não encontramos esse CEP. Confira o número ou preencha o endereço manualmente.
        </p>
      )}

      <Field id={id("city")} label="Município" required className="col-span-6 sm:col-span-3">
        <input
          id={id("city")}
          value={value.city}
          onChange={(e) => set("city", e.target.value)}
          autoComplete="address-level2"
          maxLength={100}
          disabled={disabled}
          className={input}
        />
      </Field>

      <Field id={id("neighborhood")} label="Bairro" hint="(opcional)" className="col-span-6 sm:col-span-3">
        <input
          id={id("neighborhood")}
          value={value.neighborhood}
          onChange={(e) => set("neighborhood", e.target.value)}
          autoComplete="address-level3"
          maxLength={100}
          disabled={disabled}
          className={input}
        />
      </Field>

      <Field id={id("street")} label="Rua" required className="col-span-6">
        <input
          id={id("street")}
          value={value.street}
          onChange={(e) => set("street", e.target.value)}
          autoComplete="address-line1"
          maxLength={150}
          disabled={disabled}
          className={input}
        />
      </Field>

      <Field id={id("number")} label="Número" required className="col-span-2">
        <input
          id={id("number")}
          value={value.number}
          onChange={(e) => set("number", e.target.value)}
          placeholder="S/N"
          maxLength={20}
          disabled={disabled}
          className={input}
        />
      </Field>

      <Field id={id("complement")} label="Complemento" hint="(opcional)" className="col-span-4">
        <input
          id={id("complement")}
          value={value.complement}
          onChange={(e) => set("complement", e.target.value)}
          placeholder="Apto, bloco, referência"
          autoComplete="address-line2"
          maxLength={100}
          disabled={disabled}
          className={input}
        />
      </Field>
    </div>
  );
}

function Field({
  id,
  label,
  hint,
  required,
  className = "",
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  required?: boolean;
  className?: string;
  children: ReactNode;
}) {
  return (
    <div className={`flex min-w-0 flex-col gap-1.5 ${className}`}>
      <label htmlFor={id} className="text-label-md font-semibold text-slate-700">
        {label}
        {required && <span className="ml-0.5 text-red-600">*</span>}
        {hint && <span className="ml-1 font-normal text-slate-400">{hint}</span>}
      </label>
      {children}
    </div>
  );
}