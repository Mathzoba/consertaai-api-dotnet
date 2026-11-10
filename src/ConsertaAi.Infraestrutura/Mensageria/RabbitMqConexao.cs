using ConsertaAi.Infraestrutura.Configuracoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ConsertaAi.Infraestrutura.Mensageria;

/// <summary>
/// Gerencia uma única conexão com o RabbitMQ (conexões são caras; canais são leves).
/// Reabre a conexão automaticamente se ela cair. Usada pelo publicador e pelo Health Check.
/// </summary>
public sealed class RabbitMqConexao : IDisposable
{
    private readonly ConnectionFactory _fabrica;
    private readonly RabbitMqOptions _opcoes;
    private readonly ILogger<RabbitMqConexao> _logger;
    private readonly object _trava = new();
    private IConnection? _conexao;
    private bool _topologiaDeclarada;

    public RabbitMqConexao(IOptions<RabbitMqOptions> opcoes, ILogger<RabbitMqConexao> logger)
    {
        _opcoes = opcoes.Value;
        _logger = logger;

        _fabrica = new ConnectionFactory
        {
            ClientProvidedName = "consertaai-api-dotnet",
            AutomaticRecoveryEnabled = true,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        if (!string.IsNullOrWhiteSpace(_opcoes.Uri))
        {
            _fabrica.Uri = new Uri(_opcoes.Uri);
        }
        else
        {
            _fabrica.HostName = _opcoes.HostName;
            _fabrica.Port = _opcoes.Port;
            _fabrica.VirtualHost = _opcoes.VirtualHost;
            if (!string.IsNullOrWhiteSpace(_opcoes.UserName)) _fabrica.UserName = _opcoes.UserName;
            if (!string.IsNullOrWhiteSpace(_opcoes.Password)) _fabrica.Password = _opcoes.Password;
        }
    }

    public RabbitMqOptions Opcoes => _opcoes;

    /// <summary>Retorna a conexão aberta (abrindo-a se necessário) e garante exchange/fila declaradas.</summary>
    public IConnection ObterConexao()
    {
        lock (_trava)
        {
            if (_conexao is { IsOpen: true })
                return _conexao;

            _conexao?.Dispose();
            _conexao = _fabrica.CreateConnection();
            _topologiaDeclarada = false;
            _logger.LogInformation("Conexão com o RabbitMQ estabelecida em {Host}", _conexao.Endpoint.HostName);

            DeclararTopologia(_conexao);
            return _conexao;
        }
    }

    private void DeclararTopologia(IConnection conexao)
    {
        if (_topologiaDeclarada) return;

        using var canal = conexao.CreateModel();
        canal.ExchangeDeclare(_opcoes.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);

        if (!string.IsNullOrWhiteSpace(_opcoes.FilaApoios))
        {
            canal.QueueDeclare(_opcoes.FilaApoios, durable: true, exclusive: false, autoDelete: false);
            canal.QueueBind(_opcoes.FilaApoios, _opcoes.Exchange, "apoio.*");
        }

        _topologiaDeclarada = true;
    }

    public void Dispose()
    {
        lock (_trava)
        {
            _conexao?.Dispose();
            _conexao = null;
        }
    }
}
