namespace BackendSystemVitrio.Enum
{
    // Como o cliente escolheu pagar o pedido.
    public enum OrderPaymentMethod
    {
        Arrange = 0, // combinar com a loja (WhatsApp, na entrega...): o Vitrio não participa
        Online = 1   // Mercado Pago (Checkout Pro), direto na conta da loja
    }
}
