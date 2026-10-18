// ============================================================
// WebhookRetryWorker.cs — Worker que reprocessa a Dead-Letter Queue
// A cada 5 minutos, busca webhooks com status "Failed" e tenta reenviar
// ============================================================
using ApiReliabilitySuite.Application.Services;
using ApiReliabilitySuite.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiReliabilitySuite.Workers;

public class WebhookRetryWorker : BackgroundService
{
    private readonly ILogger<WebhookRetryWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WebhookRetryWorker(ILogger<WebhookRetryWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 WebhookRetryWorker iniciado. Reprocessa dead-letter a cada 5min.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessFailedWebhooksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no WebhookRetryWorker");
            }

            // Intervalo de 5 minutos entre ciclos de reprocessamento
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessFailedWebhooksAsync(CancellationToken token)
    {
        // Cria scope para usar serviços Scoped dentro do Singleton (worker)
        using var scope = _scopeFactory.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var webhookService = scope.ServiceProvider.GetRequiredService<WebhookService>();

        // Busca todos os webhooks da Dead-Letter Queue
        var failedWebhooks = await webhookRepo.GetFailedAsync();
        var list = failedWebhooks.ToList();

        if (!list.Any())
        {
            _logger.LogDebug("Dead-letter queue vazia. Nada a reprocessar.");
            return;
        }

        _logger.LogInformation("🔄 Reprocessando {Count} webhook(s) da dead-letter queue...", list.Count);

        foreach (var webhook in list)
        {
            // Para se o app estiver encerrando
            if (token.IsCancellationRequested) break;

            _logger.LogInformation("Reprocessando webhook {Id} → {Url}", webhook.Id, webhook.TargetUrl);

            // Usa o WebhookService que já tem o Polly configurado
            await webhookService.TryDeliverAsync(webhook);
        }
    }
}
