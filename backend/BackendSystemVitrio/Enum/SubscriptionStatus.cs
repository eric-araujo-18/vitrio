namespace BackendSystemVitrio.Enum
{
    public enum SubscriptionStatus
    {
        Trial = 0,      // teste grátis (usado na etapa de cobrança)
        Active = 1,     // em dia
        PastDue = 2,    // cobrança falhou, ainda dentro da tolerância
        Suspended = 3,  // passou da tolerância: volta a valer o plano grátis
        Canceled = 4    // cancelada pelo lojista
    }
}