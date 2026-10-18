// ============================================================
// WebhookService.cs — Lógica de negócio para entrega de webhooks
//
// Responsabilidades:
// 1. Receber um webhook e persistir no banco
// 2. Tentar entregar usando Polly (retry exponencial)
// 3. Atualizar o status: Sent ou Failed (Dead-Letter)
// ============================================================
using ApiReliabilitySuite.Application.DTOs;
using ApiReliabilitySuite.Application.Policies;
using ApiReliabilitySuite.Domain.Entities;
using ApiReliabilitySuite.Domain.Interfaces;
using System.Text;

namespace ApiReliabilitySuite.Application.Services;

public class WebhookService
{
    // Repositório para persistir webhooks no banco
    private readonly IWebhookRepository _repository;

    // HttpClient para enviar o webhook ao endpoint de destino
    private readonly HttpClient _httpClient;

    // Construtor com injeção de dependência
    public WebhookService(IWebhookRepository repository, IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        // Cria o HttpClient usando o factory (melhor que new HttpClient())
        _httpClient = httpClientFactory.CreateClient("WebhookClient");
    }

    /// <summary>
    /// Recebe um novo webhook, salva no banco e tenta entregar imediatamente.
    /// Se falhar após todas as retentativas do Polly, status fica "Failed".
    /// </summary>
    public async Task<WebhookResponseDto> ReceiveAndDeliverAsync(ReceiveWebhookDto dto)
    {
        // Cria a entidade e salva imediatamente (temos registro mesmo se o app cair)
        var delivery = new WebhookDelivery
        {
            TargetUrl = dto.TargetUrl,
            Payload = dto.Payload,
            Status = DeliveryStatus.Pending
        };

        delivery = await _repository.CreateAsync(delivery);

        // Tenta entregar com Polly (pode demorar até 14s: 2+4+8)
        await TryDeliverAsync(delivery);

        return MapToDto(delivery);
    }

    /// <summary>
    /// Tenta entregar um webhook usando a política combinada do Polly.
    /// Atualiza o banco após cada tentativa (sucesso ou falha).
    /// Chamado também pelo WebhookRetryWorker ao reprocessar dead-letter.
    /// </summary>
    public async Task TryDeliverAsync(WebhookDelivery delivery)
    {
        // Obtém a política combinada (Timeout + Retry exponencial)
        var policy = PollyPolicies.GetCombinedPolicy();

        // Prepara o body JSON para enviar ao endpoint de destino
        var content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");

        try
        {
            // Executa dentro da política Polly
            // Se falhar, Polly aplica retry automaticamente antes de chegar no catch
            var response = await policy.ExecuteAsync(async () =>
            {
                // Incrementa o contador de tentativas a cada execução
                delivery.AttemptCount++;
                delivery.LastAttemptAt = DateTime.UtcNow;

                // Faz o POST para o endpoint de destino do webhook
                return await _httpClient.PostAsync(delivery.TargetUrl, content);
            });

            if (response.IsSuccessStatusCode)
            {
                // Sucesso: atualiza o status para Sent
                delivery.Status = DeliveryStatus.Sent;
                delivery.DeliveredAt = DateTime.UtcNow;
                Console.WriteLine($"[WebhookService] ✅ Webhook {delivery.Id} entregue!");
            }
            else
            {
                // Resposta não-2xx mesmo após todas as retentativas
                delivery.Status = DeliveryStatus.Failed;
                delivery.LastError = $"HTTP {(int)response.StatusCode} — {response.ReasonPhrase}";
                Console.WriteLine($"[WebhookService] ❌ Webhook {delivery.Id} falhou: {delivery.LastError}");
            }
        }
        catch (Exception ex)
        {
            // Exceção após todas as retentativas (timeout, DNS, conexão recusada)
            delivery.Status = DeliveryStatus.Failed;
            delivery.LastError = ex.Message;
            Console.WriteLine($"[WebhookService] ❌ Webhook {delivery.Id} exception: {ex.Message}");
        }

        // Sempre persiste o estado final (sucesso ou falha)
        await _repository.UpdateAsync(delivery);
    }

    // Converte entidade → DTO para retornar ao front-end
    private static WebhookResponseDto MapToDto(WebhookDelivery d) => new()
    {
        Id = d.Id,
        TargetUrl = d.TargetUrl,
        Payload = d.Payload,
        AttemptCount = d.AttemptCount,
        Status = d.Status.ToString(),
        LastError = d.LastError,
        ReceivedAt = d.ReceivedAt,
        LastAttemptAt = d.LastAttemptAt,
        DeliveredAt = d.DeliveredAt
    };
}
