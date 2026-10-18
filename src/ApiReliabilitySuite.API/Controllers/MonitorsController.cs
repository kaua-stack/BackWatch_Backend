// ============================================================
// MonitorsController.cs — Endpoints CRUD para Monitores
// GET /api/monitors         → Lista todos
// POST /api/monitors        → Cria novo
// DELETE /api/monitors/{id} → Remove
// GET /api/monitors/{id}/history → Histórico de checks
// ============================================================
using ApiReliabilitySuite.Application.DTOs;
using ApiReliabilitySuite.Domain.Entities;
using ApiReliabilitySuite.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using MonitorEntity = ApiReliabilitySuite.Domain.Entities.Monitor;

namespace ApiReliabilitySuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorsController : ControllerBase
{
    private readonly IMonitorRepository _repository;
    private readonly ILogger<MonitorsController> _logger;

    public MonitorsController(IMonitorRepository repository, ILogger<MonitorsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>GET /api/monitors — Lista todos os monitores com status atual</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MonitorResponseDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var monitors = await _repository.GetAllAsync();

        // Mapeia entidades → DTOs (nunca exponha entidades do banco diretamente!)
        var dtos = monitors.Select(m => new MonitorResponseDto
        {
            Id = m.Id,
            Name = m.Name,
            Url = m.Url,
            IntervalSeconds = m.IntervalSeconds,
            Status = m.Status.ToString(),
            CreatedAt = m.CreatedAt,
            IsActive = m.IsActive,
            // Pega o resultado mais recente (carregado pelo Include no repositório)
            LastCheck = m.Results.FirstOrDefault() is { } last
                ? new HealthCheckSummaryDto
                {
                    IsSuccess = last.IsSuccess,
                    ResponseTimeMs = last.ResponseTimeMs,
                    StatusCode = last.StatusCode,
                    CheckedAt = last.CheckedAt
                }
                : null
        });

        return Ok(dtos);
    }

    /// <summary>POST /api/monitors — Cria um novo monitor</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MonitorResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateMonitorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "O nome do monitor é obrigatório." });

        if (!IsValidMonitorUrl(dto.Url))
            return BadRequest(new { error = "URL inválida. Use o formato: https://exemplo.com" });

        if (dto.IntervalSeconds is < 15 or > 3600)
            return BadRequest(new { error = "O intervalo deve estar entre 15 e 3600 segundos." });

        var monitor = new MonitorEntity
        {
            Name = dto.Name.Trim(),
            Url = dto.Url,
            IntervalSeconds = dto.IntervalSeconds
        };

        var created = await _repository.CreateAsync(monitor);
        _logger.LogInformation("Monitor criado: {Name} → {Url}", created.Name, created.Url);

        var responseDto = new MonitorResponseDto
        {
            Id = created.Id,
            Name = created.Name,
            Url = created.Url,
            IntervalSeconds = created.IntervalSeconds,
            Status = created.Status.ToString(),
            CreatedAt = created.CreatedAt,
            IsActive = created.IsActive
        };

        // 201 Created: padrão REST para criação bem-sucedida
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, responseDto);
    }

    /// <summary>PUT /api/monitors/{id} — Atualiza um monitor existente</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateMonitorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "O nome do monitor é obrigatório." });

        if (!IsValidMonitorUrl(dto.Url))
            return BadRequest(new { error = "URL inválida. Use o formato: https://exemplo.com" });

        if (dto.IntervalSeconds is < 15 or > 3600)
            return BadRequest(new { error = "O intervalo deve estar entre 15 e 3600 segundos." });

        var updated = await _repository.UpdateAsync(
            id,
            dto.Name.Trim(),
            dto.Url,
            dto.IntervalSeconds);

        if (!updated)
            return NotFound(new { error = "Monitor não encontrado" });

        _logger.LogInformation("Monitor atualizado: {Id}", id);
        return NoContent();
    }

    /// <summary>GET /api/monitors/{id}/history — Histórico de health checks</summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var monitor = await _repository.GetByIdAsync(id);
        if (monitor is null)
            return NotFound(new { error = "Monitor não encontrado" });

        var history = await _repository.GetHistoryAsync(id, limit: 90);

        return Ok(history.Select(r => new
        {
            r.Id, r.IsSuccess, r.StatusCode,
            r.ResponseTimeMs, r.ErrorMessage, r.CheckedAt
        }));
    }

    /// <summary>DELETE /api/monitors/{id} — Remove um monitor</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var monitor = await _repository.GetByIdAsync(id);
        if (monitor is null)
            return NotFound(new { error = "Monitor não encontrado" });

        await _repository.DeleteAsync(id);
        _logger.LogInformation("Monitor removido: {Id}", id);

        // 204 No Content: operação bem-sucedida, sem corpo de resposta
        return NoContent();
    }

    private static bool IsValidMonitorUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
