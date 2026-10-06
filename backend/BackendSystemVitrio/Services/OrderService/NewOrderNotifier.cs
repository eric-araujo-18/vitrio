using System.Globalization;
using System.Net;
using System.Text;
using BackendSystemVitrio.Data;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.OrderService
{
    // Avisa o dono da loja, por e-mail, que chegou um pedido novo. O painel também avisa, mas só
    // enquanto está aberto. Pedido para combinar com a loja: avisa na criação. Pedido pago online:
    // quando o pagamento é aprovado (antes disso ele ainda pode expirar). O lojista desliga o aviso
    // em Configurações da loja (Store.NotifyNewOrdersByEmail).
    public class NewOrderNotifier
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        private readonly AppDbContext _context;
        private readonly EmailQueue _emailQueue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewOrderNotifier> _logger;

        public NewOrderNotifier(AppDbContext context, EmailQueue emailQueue, IConfiguration configuration, ILogger<NewOrderNotifier> logger)
        {
            _context = context;
            _emailQueue = emailQueue;
            _configuration = configuration;
            _logger = logger;
        }

        // Nunca lança: um e-mail que não sai não pode atrapalhar o pedido. O envio é em segundo
        // plano (EmailQueue), então isto só lê o pedido e põe o e-mail na fila.
        public async Task NotifyAsync(int orderId)
        {
            try
            {
                var order = await _context.Order.AsNoTracking()
                    .Where(o => o.Id == orderId)
                    .Select(o => new
                    {
                        o.Code,
                        o.CustomerName,
                        o.CustomerPhone,
                        o.Total,
                        o.PaymentMethod,
                        o.Notes,
                        StoreName = o.Store!.Name,
                        StoreSlug = o.Store.Slug,
                        o.Store.NotifyNewOrdersByEmail,
                        OwnerEmail = o.Store.User!.Email,
                        Items = o.Items
                            .OrderBy(i => i.Id)
                            .Select(i => new { i.ProductName, i.Size, i.Color, i.Quantity, i.UnitPrice })
                            .ToList(),
                    })
                    .FirstOrDefaultAsync();

                if (order is null || !order.NotifyNewOrdersByEmail)
                    return;

                var frontend = (_configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
                var panelLink = $"{frontend}/store/{order.StoreSlug}/shopkeeper/pedidos";

                var rows = new StringBuilder();
                foreach (var item in order.Items)
                {
                    var extras = string.Join(", ", new[] { item.Color, item.Size is null ? null : $"Tam. {item.Size}" }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                    rows.Append($"""
                        <tr>
                          <td style="padding:6px 0">{item.Quantity}× {Html(item.ProductName)}{(extras.Length > 0 ? $" <span style=\"color:#6b7280\">({Html(extras)})</span>" : "")}</td>
                          <td style="padding:6px 0;text-align:right;white-space:nowrap">{Money(item.UnitPrice * item.Quantity)}</td>
                        </tr>
                        """);
                }

                var payment = order.PaymentMethod == OrderPaymentMethod.Online
                    ? "Pago online pelo Mercado Pago. O valor já está na sua conta."
                    : "Pagamento: combinar com o cliente.";
                var notes = string.IsNullOrWhiteSpace(order.Notes)
                    ? ""
                    : $"""<p style="margin:12px 0 0"><strong>Observações:</strong> {Html(order.Notes)}</p>""";

                var html = $"""
                    <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;color:#111827">
                      <h2 style="margin-bottom:4px">Pedido novo na {Html(order.StoreName)}</h2>
                      <p style="margin-top:0;color:#6b7280">Pedido <strong>#{order.Code}</strong> de {Html(order.CustomerName)} · {Html(FormatPhone(order.CustomerPhone))}</p>
                      <table style="width:100%;border-collapse:collapse;border-top:1px solid #e5e7eb;border-bottom:1px solid #e5e7eb">
                        {rows}
                      </table>
                      <p style="text-align:right;font-size:18px;margin:12px 0 0"><strong>Total: {Money(order.Total)}</strong></p>
                      <p style="margin:12px 0 0">{payment}</p>
                      {notes}
                      <p style="margin:28px 0">
                        <a href="{Html(panelLink)}" style="background:#2563eb;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:bold">Ver o pedido no painel</a>
                      </p>
                      <p style="color:#6b7280;font-size:13px">Para não receber mais estes e-mails, desligue o aviso em Configurações da loja.</p>
                    </div>
                    """;

                if (!_emailQueue.TryEnqueue(new EmailMessage(order.OwnerEmail, $"Pedido novo #{order.Code} na {order.StoreName}", html)))
                    _logger.LogError("Fila de e-mails cheia: o aviso do pedido {OrderId} não foi enviado", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao preparar o e-mail do pedido {OrderId}", orderId);
            }
        }

        private static string Html(string value) => WebUtility.HtmlEncode(value);

        private static string Money(decimal value) => value.ToString("C", PtBr);

        // Só dígitos (como fica no pedido) -> (85) 99999-8888
        private static string FormatPhone(string digits) => digits.Length switch
        {
            11 => $"({digits[..2]}) {digits[2..7]}-{digits[7..]}",
            10 => $"({digits[..2]}) {digits[2..6]}-{digits[6..]}",
            _ => digits,
        };
    }
}
