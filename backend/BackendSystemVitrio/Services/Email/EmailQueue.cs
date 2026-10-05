using System.Threading.Channels;

namespace BackendSystemVitrio.Services.Email
{
    public record EmailMessage(string To, string Subject, string Html);

    // Fila em memória de e-mails a enviar. Quem pede o envio não espera o provedor responder:
    // a resposta da API sai na hora (e leva o mesmo tempo exista ou não a conta, no caso de
    // "esqueci minha senha"). Quem envia de verdade é o EmailBackgroundSender.
    // E-mails ainda na fila se perdem se a API reiniciar; para links de senha isso é aceitável
    // (basta pedir de novo).
    public class EmailQueue
    {
        private readonly Channel<EmailMessage> _channel =
            Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });

        // false = fila cheia (o e-mail não foi aceito)
        public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

        public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken)
            => _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
