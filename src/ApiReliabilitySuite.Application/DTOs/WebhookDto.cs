// ============================================================
// WebhookDto.cs — DTOs para Webhooks
// ============================================================
namespace ApiReliabilitySuite.Application.DTOs;

/// <summary>Resposta ao listar webhooks no dashboard</summary>
public class WebhookResponseDto
{
    public Guid Id { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string Status { get; set; } = string.Empty;     // "Pending", "Sent", "Failed"
    public string? LastError { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

/// <summary>Requisição para receber um novo webhook</summary>
public record ReceiveWebhookDto(
    string TargetUrl,   // Para onde entregar
    string Payload      // Conteúdo JSON
);
