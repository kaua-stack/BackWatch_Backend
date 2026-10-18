// ============================================================
// WebhookRepository.cs — Implementação do IWebhookRepository
// ============================================================
using ApiReliabilitySuite.Domain.Entities;
using ApiReliabilitySuite.Domain.Interfaces;
using ApiReliabilitySuite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiReliabilitySuite.Infrastructure.Repositories;

public class WebhookRepository : IWebhookRepository
{
    private readonly AppDbContext _db;

    public WebhookRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<WebhookDelivery>> GetAllAsync()
    {
        return await _db.WebhookDeliveries
            .AsNoTracking()
            .OrderByDescending(w => w.ReceivedAt)
            .ToListAsync();
    }

    public async Task<WebhookDelivery?> GetByIdAsync(Guid id)
    {
        return await _db.WebhookDeliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    /// <summary>
    /// Busca os webhooks da Dead-Letter Queue (status = Failed).
    /// Ordena pelos mais antigos primeiro (FIFO: first in, first out).
    /// </summary>
    public async Task<IEnumerable<WebhookDelivery>> GetFailedAsync()
    {
        return await _db.WebhookDeliveries
            .Where(w => w.Status == DeliveryStatus.Failed)
            .AsNoTracking()
            .OrderBy(w => w.ReceivedAt)
            .ToListAsync();
    }

    public async Task<WebhookDelivery> CreateAsync(WebhookDelivery delivery)
    {
        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync();
        return delivery;
    }

    public async Task UpdateAsync(WebhookDelivery delivery)
    {
        // Update: informa ao EF Core que a entidade foi modificada (change tracking)
        _db.WebhookDeliveries.Update(delivery);
        await _db.SaveChangesAsync();
    }
}
