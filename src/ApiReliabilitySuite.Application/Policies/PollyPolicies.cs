// ============================================================
// PollyPolicies.cs — Políticas de resiliência com Polly
//
// O que é Polly?
// É uma biblioteca .NET para lidar com falhas transitórias.
// Define como a aplicação se comporta quando algo falha:
// - Retry: tenta de novo X vezes
// - Timeout: desiste após N segundos
// - Circuit Breaker: para de tentar se falhar muitas vezes
//
// Neste projeto usamos Retry com Exponential Backoff:
// Falha 1 → aguarda 2s → tenta
// Falha 2 → aguarda 4s → tenta  
// Falha 3 → aguarda 8s → desiste → status Failed (Dead-Letter)
// ============================================================
using Polly;
using Polly.Extensions.Http;

namespace ApiReliabilitySuite.Application.Policies;

public static class PollyPolicies
{
    /// <summary>
    /// Política de Retry com Exponential Backoff.
    /// Tenta 3 vezes com espera crescente entre tentativas.
    /// Captura erros de rede e respostas HTTP não-2xx.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetWebhookRetryPolicy()
    {
        return HttpPolicyExtensions
            // HandleTransientHttpError: captura HttpRequestException e HTTP 5xx
            .HandleTransientHttpError()
            // OrResult: também captura qualquer resposta que não seja sucesso (2xx)
            .OrResult(msg => !msg.IsSuccessStatusCode)
            // WaitAndRetryAsync: aguarda e tenta novamente
            .WaitAndRetryAsync(
                retryCount: 3,  // Máximo 3 tentativas adicionais
                sleepDurationProvider: retryAttempt =>
                    // 2^1 = 2s, 2^2 = 4s, 2^3 = 8s
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log informativo a cada retentativa
                    var error = outcome.Exception?.Message
                        ?? outcome.Result?.StatusCode.ToString()
                        ?? "Erro desconhecido";
                    Console.WriteLine(
                        $"[Polly] Tentativa {retryCount}/3 falhou. " +
                        $"Aguardando {timespan.TotalSeconds}s. " +
                        $"Erro: {error}");
                }
            );
    }

    /// <summary>
    /// Política de Timeout: cancela a requisição após N segundos.
    /// Evita que o Worker trave esperando uma resposta que nunca vem.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int seconds = 10)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(seconds);
    }

    /// <summary>
    /// Política combinada: Timeout aplicado por tentativa + Retry.
    /// Ordem das camadas: Retry → Timeout → Requisição HTTP
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        return Policy.WrapAsync(
            GetWebhookRetryPolicy(),  // Camada externa: decide quantas tentativas
            GetTimeoutPolicy(10)      // Camada interna: limita o tempo de cada tentativa
        );
    }
}
