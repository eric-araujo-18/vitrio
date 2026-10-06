using System.Text.Json;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.OrderPaymentService;
using BackendSystemVitrio.Services.Payments;
using BackendSystemVitrio.Services.SubscriptionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Controllers
{
    // Notificações dos gateways de pagamento. Sem login (quem chama é o Mercado Pago).
    // As de assinatura têm a assinatura (x-signature) validada; as de pedidos só servem de aviso
    // (ver MercadoPagoOrders).
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class WebhooksController : ControllerBase
    {
        private const string Provider = "mercadopago";

        private readonly AppDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IOrderPaymentService _orderPayments;
        private readonly MercadoPagoOptions _options;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(
            AppDbContext context,
            ISubscriptionService subscriptionService,
            IOrderPaymentService orderPayments,
            IOptions<MercadoPagoOptions> options,
            ILogger<WebhooksController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _orderPayments = orderPayments;
            _options = options.Value;
            _logger = logger;
        }

        // POST /api/Webhooks/mercadopago/orders/{storeId}?data.id=...&type=payment
        // Pagamentos dos pedidos de uma loja (notification_url da preferência criada com o token
        // dela). O aviso não é confiável por si: o pagamento é lido na API do Mercado Pago com o
        // token da loja e só vale se tiver a referência de um pedido dela e o valor certo. Assim,
        // um aviso falso no máximo gera uma consulta (e a rota tem limite por IP).
        [HttpPost("mercadopago/orders/{storeId:int}")]
        [EnableRateLimiting("payment-notifications")]
        public async Task<IActionResult> MercadoPagoOrders(int storeId)
        {
            JsonElement body = default;
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body);
                body = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // corpo vazio/inválido: segue só com o que veio na URL
            }

            // Formato webhook (data.id + type) ou o antigo IPN (id + topic).
            string? paymentId = Request.Query["data.id"];
            string? type = Request.Query["type"];
            if (string.IsNullOrEmpty(paymentId) && TryGetProperty(body, "data", out var data))
                paymentId = ReadScalar(data, "id");
            type ??= ReadScalar(body, "type") ?? Request.Query["topic"];
            if (string.IsNullOrEmpty(paymentId) && type == "payment")
                paymentId = Request.Query["id"];

            if (type != "payment" || string.IsNullOrEmpty(paymentId) || paymentId.Length > 30)
                return Ok(); // outros avisos (ex.: merchant_order) não mudam nada aqui

            try
            {
                await _orderPayments.HandleNotificationAsync(storeId, paymentId);
                return Ok();
            }
            catch (Exception ex)
            {
                // 500 faz o Mercado Pago tentar de novo (a manutenção também confere sozinha).
                _logger.LogError(ex, "Erro ao processar o aviso do pagamento {PaymentId} da loja {StoreId}", paymentId, storeId);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        // POST /api/Webhooks/mercadopago?data.id=...&type=...
        [HttpPost("mercadopago")]
        public async Task<IActionResult> MercadoPago()
        {
            // Lê o corpo "na mão": se vier vazio ou fora do formato, não derruba a
            // requisição com 400 automático (o que esconderia o problema).
            JsonElement body = default;
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body);
                body = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // corpo vazio/inválido: segue só com o que veio na URL
            }
            // data.id e type vêm na URL; o corpo tem os mesmos dados (e o id da notificação).
            // O corpo é lido com cuidado: um campo com tipo inesperado (ex.: "data" que não é
            // objeto) vira "ausente", em vez de lançar exceção e responder 500 (o que faria o
            // Mercado Pago reenviar a mesma notificação várias vezes).
            string? dataId = Request.Query["data.id"];
            string? type = Request.Query["type"];

            if (string.IsNullOrEmpty(dataId) && TryGetProperty(body, "data", out var data))
                dataId = ReadScalar(data, "id");
            if (string.IsNullOrEmpty(type))
                type = ReadScalar(body, "type");

            var action = ReadScalar(body, "action");
            var notificationId = ReadScalar(body, "id");
            var requestId = Request.Headers["x-request-id"].ToString();

            _logger.LogInformation("Webhook do Mercado Pago recebido: type={Type} action={Action} data.id={DataId}", type, action, dataId);

            if (string.IsNullOrEmpty(dataId) || string.IsNullOrEmpty(type))
            {
                _logger.LogWarning("Webhook ignorado: sem type ou data.id. Query={Query}", Request.QueryString.Value);
                return BadRequest();
            }

            // 1) Autenticidade
            if (!MercadoPagoSignature.IsValid(Request.Headers["x-signature"], requestId, dataId, _options.WebhookSecret))
            {
                _logger.LogWarning("Notificação do Mercado Pago com assinatura inválida (type={Type}, data.id={DataId})", type, dataId);
                return Unauthorized();
            }

            // 2) Idempotência: o mesmo aviso pode chegar mais de uma vez.
            notificationId = string.IsNullOrEmpty(notificationId) ? $"{type}:{dataId}:{requestId}" : notificationId;

            var evt = await _context.PaymentWebhookEvent
                .FirstOrDefaultAsync(e => e.Provider == Provider && e.NotificationId == notificationId);

            if (evt?.ProcessedAt is not null)
                return Ok();

            if (evt is null)
            {
                evt = new PaymentWebhookEvent
                {
                    Provider = Provider,
                    NotificationId = notificationId,
                    Type = type,
                    Action = action,
                    DataId = dataId,
                };
                _context.PaymentWebhookEvent.Add(evt);
                await _context.SaveChangesAsync();
            }

            // 3) Processa. Se falhar, responde 500 para o Mercado Pago tentar de novo.
            try
            {
                switch (type)
                {
                    case "subscription_preapproval":
                        await _subscriptionService.SyncPreapprovalAsync(dataId);
                        break;

                    case "subscription_authorized_payment":
                        await _subscriptionService.HandleAuthorizedPaymentAsync(dataId);
                        break;

                    // Outros tipos (ex: "payment" das vendas) entram na próxima etapa.
                }

                evt.ProcessedAt = DateTime.UtcNow;
                evt.Error = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Webhook {NotificationId} processado ({Type})", notificationId, type);
                return Ok();
            }
            catch (MercadoPagoException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
            {
                // O recurso não existe no Mercado Pago (ex: "simular notificação" do painel,
                // que usa um id de exemplo). Tentar de novo nunca vai funcionar, então
                // responde 200 para o Mercado Pago parar de reenviar e registra o motivo.
                _logger.LogWarning("Notificação {NotificationId}: {Type} {DataId} não existe no Mercado Pago", notificationId, type, dataId);
                evt.ProcessedAt = DateTime.UtcNow;
                evt.Error = "Recurso não encontrado no Mercado Pago (404).";
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar notificação {NotificationId} ({Type})", notificationId, type);
                evt.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                await _context.SaveChangesAsync();
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
        {
            value = default;
            return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out value);
        }

        // Texto ou número; qualquer outro tipo (objeto, lista, null...) conta como ausente.
        private static string? ReadScalar(JsonElement obj, string name)
        {
            if (!TryGetProperty(obj, name, out var value))
                return null;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
        }
    }
}