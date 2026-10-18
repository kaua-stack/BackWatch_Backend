// ============================================================
// WebhookDelivery.cs — Entidade: tentativa de entrega de webhook
// Quando falha após retentativas, fica na Dead-Letter Queue
// ============================================================
namespace ApiReliabilitySuite.Domain.Entities;

public class WebhookDelivery
{
    // Chave primária
    public Guid Id { get; set; } = Guid.NewGuid();

    // URL de destino (endpoint do cliente que recebe o webhook)
    public string TargetUrl { get; set; } = string.Empty;

    // Corpo JSON do webhook (payload)
    public string Payload { get; set; } = string.Empty;

    // Quantas vezes já tentou entregar (incrementa a cada tentativa)
    public int AttemptCount { get; set; } = 0;

    // Estado atual da entrega
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    // Mensagem de erro da última tentativa (ex: "Connection refused")
    public string? LastError { get; set; }

    // Quando o webhook foi recebido pela nossa API
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Quando foi a última tentativa de entrega
    public DateTime? LastAttemptAt { get; set; }

    // Quando foi entregue com sucesso (null se ainda não entregou)
    public DateTime? DeliveredAt { get; set; }
}
