// ============================================================
// IMonitorRepository.cs — Contrato do repositório de Monitor
// Controllers e Workers dependem desta interface, não da classe concreta
// Isso permite trocar PostgreSQL por outro banco sem mudar a lógica
// ============================================================
using ApiReliabilitySuite.Domain.Entities;
using MonitorEntity = ApiReliabilitySuite.Domain.Entities.Monitor;

namespace ApiReliabilitySuite.Domain.Interfaces;

public interface IMonitorRepository
{
    // Lista todos os monitores (com o último resultado incluso)
    Task<IEnumerable<MonitorEntity>> GetAllAsync();

    // Busca um monitor específico pelo ID
    Task<MonitorEntity?> GetByIdAsync(Guid id);

    // Busca os últimos N health checks de um monitor
    Task<IEnumerable<HealthCheckResult>> GetHistoryAsync(Guid monitorId, int limit = 90);

    // Cria um novo monitor no banco
    Task<MonitorEntity> CreateAsync(MonitorEntity monitor);

    // Atualiza os campos configuráveis de um monitor
    Task<bool> UpdateAsync(Guid id, string name, string url, int intervalSeconds);

    // Atualiza apenas o Status (não carrega a entidade inteira)
    Task UpdateStatusAsync(Guid monitorId, MonitorStatus status);

    // Salva um resultado de health check
    Task SaveHealthCheckResultAsync(HealthCheckResult result);

    // Remove um monitor e todos seus resultados (CASCADE)
    Task DeleteAsync(Guid id);

    // Retorna só os monitores com IsActive = true (usado pelo Worker)
    Task<IEnumerable<MonitorEntity>> GetActiveMonitorsAsync();
}
