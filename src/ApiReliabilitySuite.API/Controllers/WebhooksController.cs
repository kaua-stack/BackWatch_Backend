// ============================================================
// WebhooksController.cs — Endpoints para gerenciar Webhooks
// GET /api/webhooks           → Lista todos
// POST /api/webhooks          → Recebe novo webhook
// POST /api/webhooks/{id}/retry → Força reenvio manual
// ============================================================
using ApiReliabilitySuite.Application.DTOs;
using ApiReliabilitySuite.Application.Services;
using ApiReliabilitySuite.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiReliabilitySuite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookRepository _repository;
    private readonly WebhookService _webhookService;

    public WebhooksController(IWebhookRepository repository, WebhookService webhookService)
    {
        _repository = repository;
        _webhookService = webhookService;
    }

    /// <summary>GET /api/webhooks — Lista todos os webhooks (incluindo dead-letter)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var webhooks = await _repository.GetAllAsync();
        return Ok(webhooks.Select(w => new WebhookResponseDto
        {
            Id = w.Id,
            TargetUrl = w.TargetUrl,
            Payload = w.Payload,
            AttemptCount = w.AttemptCount,
            Status = w.Status.ToString(),
            LastError = w.LastError,
            ReceivedAt = w.ReceivedAt,
            LastAttemptAt = w.LastAttemptAt,
            DeliveredAt = w.DeliveredAt
        }));
    }

    /// <summary>POST /api/webhooks — Recebe e tenta entregar um webhook</summary>
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] ReceiveWebhookDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TargetUrl) || string.IsNullOrWhiteSpace(dto.Payload))
            return BadRequest(new { error = "TargetUrl e Payload são obrigatórios" });

        // 202 Accepted: indica que o processamento foi iniciado (pode demorar pelo Polly)
        var result = await _webhookService.ReceiveAndDeliverAsync(dto);
        return Accepted(result);
    }

    /// <summary>POST /api/webhooks/{id}/retry — Botão "Retry" do dashboard</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Retry(Guid id)
    {
        var webhook = await _repository.GetByIdAsync(id);
        if (webhook is null)
            return NotFound(new { error = "Webhook não encontrado" });

        // Força nova tentativa de entrega com Polly
        await _webhookService.TryDeliverAsync(webhook);

        // Busca o status atualizado após a tentativa
        var updated = await _repository.GetByIdAsync(id);
        return Ok(new
        {
            status = updated!.Status.ToString(),
            message = updated.Status.ToString() == "Sent"
                ? "✅ Webhook entregue com sucesso!"
                : "❌ Falhou novamente. Verifique o endpoint de destino."
        });
    }
}
