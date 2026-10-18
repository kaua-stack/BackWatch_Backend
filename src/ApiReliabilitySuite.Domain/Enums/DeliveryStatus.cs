// ============================================================
// DeliveryStatus.cs — Enum: ciclo de vida de um webhook
// ============================================================
namespace ApiReliabilitySuite.Domain.Entities;

public enum DeliveryStatus
{
    Pending  = 0,   // Aguardando entrega
    Sent     = 1,   // Entregue com sucesso
    Failed   = 2,   // Falhou (Dead-Letter Queue)
    Retrying = 3    // Sendo reprocessado pelo Worker
}
