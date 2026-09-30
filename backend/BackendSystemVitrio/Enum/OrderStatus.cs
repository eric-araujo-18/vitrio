namespace BackendSystemVitrio.Enum
{
    public enum OrderStatus
    {
        Pending = 0,    // recebido, aguardando o lojista
        Confirmed = 1,  // lojista aceitou
        Shipped = 2,    // enviado / saiu para entrega
        Delivered = 3,  // entregue (finalizado)
        Canceled = 4    // cancelado (estoque é devolvido)
    }
}
