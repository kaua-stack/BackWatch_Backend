// ============================================================
// MonitorDto.cs — DTOs para Monitores
// DTOs (Data Transfer Objects) evitam expor entidades do banco
// O front-end recebe/envia estes objetos, nunca as entidades
// ============================================================
namespace ApiReliabilitySuite.Application.DTOs;

/// <summary>Resumo do último health check (incluso no MonitorResponseDto)</summary>
public class HealthCheckSummaryDto
{
    public bool IsSuccess { get; set; }
    public long ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public DateTime CheckedAt { get; set; }
}

/// <summary>Resposta da API ao listar/criar monitores</summary>
public class MonitorResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; }
    public string Status { get; set; } = string.Empty;      // "Up", "Down", "Degraded"
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public HealthCheckSummaryDto? LastCheck { get; set; }   // Pode ser null no primeiro check
}

/// <summary>Requisição para criar um monitor (enviado pelo React)</summary>
public record CreateMonitorDto(
    string Name,
    string Url,
    int IntervalSeconds = 60
);
