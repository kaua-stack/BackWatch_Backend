// ============================================================
// HealthCheckWorker.cs — Worker Service que monitora as URLs
//
// Como funciona:
// 1. Fica em loop infinito (enquanto o app roda)
// 2. A cada 30s busca os monitores ativos do banco
// 3. Para cada monitor, faz um GET na URL configurada
// 4. Salva o resultado (StatusCode, tempo, erro) no banco
// 5. Atualiza o status do monitor (Up/Down/Degraded)
//
// BackgroundService: classe base do .NET para workers
// IServiceScopeFactory: cria scopes para usar DbContext (que é Scoped)
// ============================================================
using ApiReliabilitySuite.Domain.Entities;
using ApiReliabilitySuite.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using MonitorEntity = ApiReliabilitySuite.Domain.Entities.Monitor;

namespace ApiReliabilitySuite.Workers;

public class HealthCheckWorker : BackgroundService
{
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<Guid, DateTime> _nextChecks = new();

    public HealthCheckWorker(
        ILogger<HealthCheckWorker> logger,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// ExecuteAsync: método principal — roda até o app ser encerrado.
    /// CancellationToken: sinaliza quando deve parar (ex: Ctrl+C).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔍 HealthCheckWorker iniciado. Respeitando o intervalo configurado em cada monitor.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllMonitorsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Captura erros inesperados para o loop não parar
                _logger.LogError(ex, "Erro no ciclo do HealthCheckWorker");
            }

            await Task.Delay(GetNextDelay(), stoppingToken);
        }

        _logger.LogInformation("HealthCheckWorker encerrado.");
    }

    private async Task CheckAllMonitorsAsync(CancellationToken token)
    {
        // Cria um scope DI para usar DbContext (que é Scoped, não Singleton)
        // Sem isso, teríamos DbContext compartilhado entre threads — perigoso!
        using var scope = _scopeFactory.CreateScope();
        var monitorRepo = scope.ServiceProvider.GetRequiredService<IMonitorRepository>();

        var monitors = await monitorRepo.GetActiveMonitorsAsync();
        var monitorList = monitors.ToList();

        if (monitorList.Count == 0)
        {
            _nextChecks.Clear();
            _logger.LogDebug("Nenhum monitor ativo encontrado.");
            return;
        }

        var activeIds = monitorList.Select(m => m.Id).ToHashSet();
        foreach (var scheduledId in _nextChecks.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _nextChecks.Remove(scheduledId);

        var now = DateTime.UtcNow;
        var dueMonitors = monitorList
            .Where(m => !_nextChecks.TryGetValue(m.Id, out var nextCheck) || nextCheck <= now)
            .ToList();

        if (dueMonitors.Count == 0)
            return;

        _logger.LogInformation("Verificando {Count} monitor(es)...", dueMonitors.Count);

        foreach (var monitor in dueMonitors)
        {
            var intervalSeconds = Math.Clamp(monitor.IntervalSeconds, 15, 3600);
            _nextChecks[monitor.Id] = now.AddSeconds(intervalSeconds);
        }

        var tasks = dueMonitors.Select(m => CheckSingleMonitorAsync(m, monitorRepo, token));
        await Task.WhenAll(tasks);
    }

    private TimeSpan GetNextDelay()
    {
        if (_nextChecks.Count == 0)
            return TimeSpan.FromSeconds(30);

        var untilNextCheck = _nextChecks.Values.Min() - DateTime.UtcNow;
        return untilNextCheck <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : untilNextCheck > TimeSpan.FromSeconds(30)
                ? TimeSpan.FromSeconds(30)
                : untilNextCheck;
    }

    private async Task CheckSingleMonitorAsync(
        MonitorEntity monitor,
        IMonitorRepository repo,
        CancellationToken token)
    {
        var httpClient = _httpClientFactory.CreateClient("HealthCheck");
        var stopwatch = Stopwatch.StartNew();   // Mede o tempo de resposta
        var result = new HealthCheckResult
        {
            MonitorId = monitor.Id,
            CheckedAt = DateTime.UtcNow
        };

        try
        {
            // Define timeout de 10s para este check específico
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            // Executa o GET na URL do monitor
            var response = await httpClient.GetAsync(monitor.Url, cts.Token);
            stopwatch.Stop();

            // Preenche o resultado com os dados da resposta HTTP
            result.StatusCode = (int)response.StatusCode;
            result.IsSuccess = response.IsSuccessStatusCode;
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            // Determina o novo status baseado na resposta
            var newStatus = DetermineStatus(result.StatusCode, result.ResponseTimeMs);

            // Salva o resultado no banco
            await repo.SaveHealthCheckResultAsync(result);

            // Atualiza o status do monitor se mudou (evita UPDATE desnecessário)
            if (monitor.Status != newStatus)
            {
                await repo.UpdateStatusAsync(monitor.Id, newStatus);
                _logger.LogWarning(
                    "🔔 Monitor '{Name}' mudou: {Old} → {New}",
                    monitor.Name, monitor.Status, newStatus);
            }

            _logger.LogInformation(
                "✅ [{Name}] {Status} — HTTP {Code} em {Ms}ms",
                monitor.Name, newStatus, result.StatusCode, result.ResponseTimeMs);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            stopwatch.Stop();
            // Falha de conexão, timeout ou DNS
            result.IsSuccess = false;
            result.StatusCode = 0;              // 0 = sem resposta HTTP
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            result.ErrorMessage = ex.Message;

            await repo.SaveHealthCheckResultAsync(result);
            await repo.UpdateStatusAsync(monitor.Id, MonitorStatus.Down);

            _logger.LogError("❌ [{Name}] FORA DO AR: {Error}", monitor.Name, ex.Message);
        }
    }

    /// <summary>
    /// Calcula o status baseado no código HTTP e tempo de resposta.
    /// Regras: 0 → Down, 5xx → Down, >3000ms → Degraded, 2xx → Up
    /// </summary>
    private static MonitorStatus DetermineStatus(int statusCode, long responseTimeMs)
    {
        if (statusCode == 0) return MonitorStatus.Down;
        if (statusCode >= 500) return MonitorStatus.Down;
        if (responseTimeMs > 3000) return MonitorStatus.Degraded;
        if (statusCode is >= 200 and < 300) return MonitorStatus.Up;
        return MonitorStatus.Degraded;
    }
}
