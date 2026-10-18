// ============================================================
// HealthCheckResult.cs — Resultado de cada verificação de saúde
// O Worker cria um registro deste para cada ping realizado
// ============================================================
namespace ApiReliabilitySuite.Domain.Entities;

public class HealthCheckResult
{
    // Chave primária
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK: qual monitor originou este resultado
    public Guid MonitorId { get; set; }

    // Propriedade de navegação EF Core (Monitor pai)
    public Monitor Monitor { get; set; } = null!;

    // Código HTTP retornado (200, 500, 0 = sem resposta/timeout)
    public int StatusCode { get; set; }

    // true se StatusCode for 2xx (sucesso)
    public bool IsSuccess { get; set; }

    // Tempo de resposta em milissegundos (medido com Stopwatch)
    public long ResponseTimeMs { get; set; }

    // Mensagem de erro caso a requisição falhe
    public string? ErrorMessage { get; set; }

    // Quando este check foi realizado (sempre UTC)
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
