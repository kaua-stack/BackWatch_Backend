// ============================================================
// IWebhookRepository.cs — Contrato do repositório de Webhook
// ============================================================
using ApiReliabilitySuite.Domain.Entities;

namespace ApiReliabilitySuite.Domain.Interfaces;

public interface IWebhookRepository
{
    // Lista todos os webhooks (mais recentes primeiro)
    Task<IEnumerable<WebhookDelivery>> GetAllAsync();

    // Busca um webhook pelo ID
    Task<WebhookDelivery?> GetByIdAsync(Guid id);

    // Busca apenas os webhooks com status "Failed" (Dead-Letter Queue)
    Task<IEnumerable<WebhookDelivery>> GetFailedAsync();

    // Cria um novo registro de webhook
    Task<WebhookDelivery> CreateAsync(WebhookDelivery delivery);

    // Atualiza um webhook após tentativa de entrega
    Task UpdateAsync(WebhookDelivery delivery);
}
