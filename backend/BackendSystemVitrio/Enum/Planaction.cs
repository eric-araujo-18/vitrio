namespace BackendSystemVitrio.Enum
{
    // O que o lojista pode fazer com cada plano, decidido SEMPRE pelo backend.
    // O front só desenha o botão que vier aqui; se alguém chamar a API "na mão"
    // com outra coisa, o backend aplica a mesma regra e recusa.
    public enum PlanAction
    {
        Current = 0,            // é o plano atual (sem botão)
        Checkout = 1,           // assinar / fazer upgrade: abre o pagamento no Mercado Pago
        ScheduleDowngrade = 2,  // plano menor: troca no próximo ciclo, sem pagar agora
        KeepCurrent = 3,        // desistir do downgrade agendado (botão no plano atual)
        CancelToFree = 4,       // no card do Grátis: cancelar a assinatura paga
        Unavailable = 5         // não pode agora (o motivo vem no texto do botão)
    }
}