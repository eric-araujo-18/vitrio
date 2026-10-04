using System.Security.Cryptography;
using System.Text;

namespace BackendSystemVitrio.Services.Payments
{
    // Valida o header x-signature das notificações do Mercado Pago.
    // Formato: "ts=1704908010,v1=<hmac sha256 em hex>"
    // Texto assinado: "id:{data.id da URL};request-id:{header x-request-id};ts:{ts};"
    public static class MercadoPagoSignature
    {
        public static bool IsValid(string? xSignature, string? xRequestId, string? dataId, string secret)
        {
            if (string.IsNullOrWhiteSpace(xSignature) || string.IsNullOrWhiteSpace(secret))
                return false;

            string? ts = null, v1 = null;
            foreach (var part in xSignature.Split(','))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                if (key == "ts") ts = kv[1].Trim();
                else if (key == "v1") v1 = kv[1].Trim();
            }

            if (ts is null || v1 is null)
                return false;

            // Monta o manifest só com as partes presentes, como na documentação.
            var manifest = new StringBuilder();
            if (!string.IsNullOrEmpty(dataId))
                manifest.Append("id:").Append(dataId.ToLowerInvariant()).Append(';'); // id alfanumérico vai em minúsculas
            if (!string.IsNullOrEmpty(xRequestId))
                manifest.Append("request-id:").Append(xRequestId).Append(';');
            manifest.Append("ts:").Append(ts).Append(';');

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString()))).ToLowerInvariant();

            // Comparação em tempo constante.
            // Reenvio de uma notificação antiga não é perigoso: o processamento é
            // idempotente e sempre consulta o estado atual na API do Mercado Pago.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
        }
    }
}