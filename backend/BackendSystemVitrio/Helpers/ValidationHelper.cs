namespace BackendSystemVitrio.Helpers
{
    // Validações de dados pessoais usadas por vários services (cadastro, perfil, lojas, pedidos).
    // As mesmas regras existem no frontend (lib/validators.tsx); aqui elas valem de verdade.
    public static class ValidationHelper
    {
        public const int MaxEmailLength = 254;
        // Mesmo limite dos campos de nome no frontend.
        public const int MaxNameLength = 100;

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length is < 5 or > MaxEmailLength || !email.Contains('@'))
                return false;
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                // "nome <a@b.com>" também é aceito pelo MailAddress: só vale o endereço puro.
                return address.Address == email && address.Host.Contains('.');
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // CPF só com dígitos: tamanho, sequência repetida (111.111.111-11 passa no formato, mas
        // nunca é válido) e os dois dígitos verificadores.
        public static bool IsValidCpf(string? digits)
        {
            if (digits is null || digits.Length != 11 || !digits.All(char.IsAsciiDigit) || digits.Distinct().Count() == 1)
                return false;

            static int Digit(string numbers, int firstWeight)
            {
                var sum = 0;
                for (var i = 0; i < numbers.Length; i++)
                    sum += (numbers[i] - '0') * (firstWeight - i);
                var remainder = sum * 10 % 11;
                return remainder == 10 ? 0 : remainder;
            }

            var first = Digit(digits[..9], 10);
            var second = Digit(digits[..9] + first, 11);
            return digits[9] - '0' == first && digits[10] - '0' == second;
        }

        // CNPJ só com dígitos: tamanho, sequência repetida e os dois dígitos verificadores
        // (pesos da Receita Federal).
        public static bool IsValidCnpj(string? digits)
        {
            if (digits is null || digits.Length != 14 || !digits.All(char.IsAsciiDigit) || digits.Distinct().Count() == 1)
                return false;

            static int Digit(string numbers, int[] weights)
            {
                var sum = 0;
                for (var i = 0; i < numbers.Length; i++)
                    sum += (numbers[i] - '0') * weights[i];
                var remainder = sum % 11;
                return remainder < 2 ? 0 : 11 - remainder;
            }

            int[] firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
            int[] secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
            var first = Digit(digits[..12], firstWeights);
            var second = Digit(digits[..12] + first, secondWeights);
            return digits[12] - '0' == first && digits[13] - '0' == second;
        }
    }
}
