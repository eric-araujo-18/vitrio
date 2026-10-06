"use client";

import { useRef, type MouseEvent, type PointerEvent } from "react";

/**
 * Props do fundo escuro de um modal ou gaveta: fecha ao clicar fora, mas só quando o botão do
 * mouse desce E sobe no próprio fundo.
 *
 * Com um onClick simples, selecionar o texto de um campo arrastando o mouse até fora do modal
 * fechava tudo: o navegador entrega o "clique" ao elemento em comum entre onde o botão desceu
 * (o campo) e onde subiu (o fundo), que é o próprio fundo. Junto ia o que já tinha sido digitado.
 * O mesmo vale para o arrasto ao contrário (começa no fundo e termina no modal).
 *
 *   const backdrop = useBackdropDismiss(() => !sending && onClose());
 *   <div className={overlay} {...backdrop}>
 */
export function useBackdropDismiss(onDismiss: () => void) {
  const pressedOnBackdrop = useRef(false);
  const releasedOnBackdrop = useRef(false);

  return {
    onPointerDown: (e: PointerEvent<HTMLElement>) => {
      pressedOnBackdrop.current = e.target === e.currentTarget;
      releasedOnBackdrop.current = false;
    },
    onPointerUp: (e: PointerEvent<HTMLElement>) => {
      releasedOnBackdrop.current = e.target === e.currentTarget;
    },
    onClick: (e: MouseEvent<HTMLElement>) => {
      const dismiss = pressedOnBackdrop.current && releasedOnBackdrop.current && e.target === e.currentTarget;
      pressedOnBackdrop.current = releasedOnBackdrop.current = false;
      if (dismiss) onDismiss();
    },
  };
}
