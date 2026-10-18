// ============================================================
// Program.cs — Entry point dos Workers (.NET Worker Service)
// Configura DI e registra os BackgroundServices
// ============================================================
using ApiReliabilitySuite.Application.Services;
using ApiReliabilitySuite.Domain.Interfaces;
using ApiReliabilitySuite.Infrastructure.Data;
using ApiReliabilitySuite.Infrastructure.Repositories;
using ApiReliabilitySuite.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // ── Banco de Dados ────────────────────────────────────────────
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string não configurada!");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ── Repositórios ──────────────────────────────────────────────
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();

        // ── Serviços de Aplicação ────────────────────────────────────
        services.AddScoped<WebhookService>();

        // ── HttpClient ────────────────────────────────────────────────
        services.AddHttpClient("HealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiReliabilitySuite/1.0 HealthCheck");
        });

        services.AddHttpClient("WebhookClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Workers (Background Services) ─────────────────────────────
        // AddHostedService: registra o worker para rodar em background
        services.AddHostedService<HealthCheckWorker>();
        services.AddHostedService<WebhookRetryWorker>();
    })
    .Build();

// Aplica migrations pendentes ao iniciar (garante banco atualizado)
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

await host.RunAsync();
