// ============================================================
// MonitorStatus.cs — Enum: possíveis estados de um monitor
// Up = verde, Down = vermelho, Degraded = amarelo
// ============================================================
namespace ApiReliabilitySuite.Domain.Entities;

public enum MonitorStatus
{
    Unknown  = 0,   // Estado inicial (nunca verificado)
    Up       = 1,   // Serviço operacional
    Down     = 2,   // Serviço fora do ar
    Degraded = 3    // Serviço lento ou com erros parciais
}
