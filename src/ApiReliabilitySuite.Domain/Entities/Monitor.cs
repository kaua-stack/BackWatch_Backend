// ============================================================
// Monitor.cs — Entidade: URL monitorada pelo sistema
// Cada registro = uma URL que o Worker vai verificar periodicamente
// ============================================================
namespace ApiReliabilitySuite.Domain.Entities;

public class Monitor
{
    // Chave primária — GUID gerado automaticamente
    public Guid Id { get; set; } = Guid.NewGuid();

    // Nome amigável exibido no dashboard (ex: "API de Pagamentos")
    public string Name { get; set; } = string.Empty;

    // URL que será verificada (ex: https://api.minhaempresa.com/health)
    public string Url { get; set; } = string.Empty;

    // Intervalo em segundos entre cada verificação (padrão: 60s)
    public int IntervalSeconds { get; set; } = 60;

    // Status atual calculado pelo Worker após cada verificação
    public MonitorStatus Status { get; set; } = MonitorStatus.Unknown;

    // Data de criação em UTC (sempre UTC para consistência global)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Se false, o Worker ignora este monitor (usuário pausou)
    public bool IsActive { get; set; } = true;

    // Navegação EF Core: um Monitor tem muitos resultados de health check
    public ICollection<HealthCheckResult> Results { get; set; } = new List<HealthCheckResult>();
}
