namespace BackendSystemVitrio.DTO
{
    // "Esqueci minha senha": pede o link por e-mail.
    public class ForgotPasswordDto
    {
        public string? Email { get; set; }

        // Slug da loja, quando o pedido vem da vitrine (o cliente). O link do e-mail
        // leva de volta para essa loja depois de redefinir a senha.
        public string? StoreSlug { get; set; }
    }

    // Redefinição com o token que veio no link do e-mail.
    public class ResetPasswordDto
    {
        public string? Token { get; set; }
        public string? NewPassword { get; set; }
    }
}
