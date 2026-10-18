// ============================================================
// MonitorRepository.cs — Implementação concreta do IMonitorRepository
// Usa EF Core para executar queries no PostgreSQL
// ============================================================
using ApiReliabilitySuite.Domain.Entities;
using ApiReliabilitySuite.Domain.Interfaces;
using ApiReliabilitySuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MonitorEntity = ApiReliabilitySuite.Domain.Entities.Monitor;

namespace ApiReliabilitySuite.Infrastructure.Repositories;

public class MonitorRepository : IMonitorRepository
{
    // DbContext injetado via DI (uma instância por requisição HTTP)
    private readonly AppDbContext _db;

    public MonitorRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Busca todos os monitores com o último resultado incluso.
    /// Include: carrega a relação Results (apenas 1 resultado, o mais recente)
    /// AsNoTracking: melhora performance para leituras (sem change tracking)
    /// </summary>
    public async Task<IEnumerable<MonitorEntity>> GetAllAsync()
    {
        return await _db.Monitors
            .Include(m => m.Results
                .OrderByDescending(r => r.CheckedAt)
                .Take(1))
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<MonitorEntity?> GetByIdAsync(Guid id)
    {
        return await _db.Monitors
            .Include(m => m.Results
                .OrderByDescending(r => r.CheckedAt)
                .Take(10))
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    /// <summary>
    /// Retorna o histórico de health checks para renderizar gráficos no dashboard.
    /// </summary>
    public async Task<IEnumerable<HealthCheckResult>> GetHistoryAsync(Guid monitorId, int limit = 90)
    {
        return await _db.HealthCheckResults
            .Where(r => r.MonitorId == monitorId)
            .OrderByDescending(r => r.CheckedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<MonitorEntity> CreateAsync(MonitorEntity monitor)
    {
        _db.Monitors.Add(monitor);
        await _db.SaveChangesAsync();
        return monitor;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string url, int intervalSeconds)
    {
        var updatedCount = await _db.Monitors
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Name, name)
                .SetProperty(m => m.Url, url)
                .SetProperty(m => m.IntervalSeconds, intervalSeconds));

        return updatedCount > 0;
    }

    /// <summary>
    /// Atualiza APENAS o Status sem carregar a entidade inteira.
    /// ExecuteUpdateAsync: gera um UPDATE direto no banco (mais eficiente).
    /// </summary>
    public async Task UpdateStatusAsync(Guid monitorId, MonitorStatus status)
    {
        await _db.Monitors
            .Where(m => m.Id == monitorId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, status));
    }

    public async Task SaveHealthCheckResultAsync(HealthCheckResult result)
    {
        _db.HealthCheckResults.Add(result);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        // ExecuteDeleteAsync: DELETE direto sem carregar a entidade
        await _db.Monitors.Where(m => m.Id == id).ExecuteDeleteAsync();
    }

    /// <summary>Retorna monitores ativos para o Worker verificar.</summary>
    public async Task<IEnumerable<MonitorEntity>> GetActiveMonitorsAsync()
    {
        return await _db.Monitors
            .Where(m => m.IsActive)
            .AsNoTracking()
            .ToListAsync();
    }
}
