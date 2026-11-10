namespace ConsertaAi.Infraestrutura.Configuracoes;

/// <summary>Seção "RabbitMq". Credenciais SEMPRE via variável de ambiente.</summary>
public sealed class RabbitMqOptions
{
    public const string Secao = "RabbitMq";

    /// <summary>Opcional: amqp(s)://usuario:senha@host:porta/vhost (ex.: CloudAMQP). Tem precedência sobre os campos abaixo.</summary>
    public string? Uri { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string VirtualHost { get; set; } = "/";

    /// <summary>Exchange (topic) compartilhada com a API Java.</summary>
    public string Exchange { get; set; } = "consertaai.eventos";

    /// <summary>Fila declarada e ligada a "apoio.*" para que nenhum evento se perca antes de a Java subir.</summary>
    public string? FilaApoios { get; set; } = "consertaai.apoios";
}
