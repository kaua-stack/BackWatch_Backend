namespace ApiReliabilitySuite.API.Mock;

public static class MockApiEndpoints
{
    public static void MapMockEndpoints(this WebApplication app)
    {
        var mock = app.MapGroup("/mock").WithTags("🎭 Mock / Teste");

        mock.MapGet("/ok", () => Results.Ok(new
        {
            status = "healthy",
            message = "API funcionando perfeitamente!",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        }))
        .WithName("MockOk")
        .WithSummary("Simula API saudável → HTTP 200");

        mock.MapGet("/error", () => Results.Problem(
            title: "Internal Server Error",
            detail: "Simulação de erro crítico na API do cliente",
            statusCode: 500))
        .WithName("MockError")
        .WithSummary("Simula API com falha → HTTP 500");

        mock.MapGet("/slow", async () =>
        {
            await Task.Delay(4000);
            return Results.Ok(new
            {
                status = "degraded",
                message = "API respondeu, mas com lentidão!",
                timestamp = DateTime.UtcNow,
                delayMs = 4000
            });
        })
        .WithName("MockSlow")
        .WithSummary("Simula API lenta (4s delay) → HTTP 200 degradado");

        mock.MapGet("/custom", async (int statusCode = 200, int delayMs = 0) =>
        {
            if (delayMs > 0)
                await Task.Delay(Math.Min(delayMs, 10000));

            return Results.Json(
                new { statusCode, delayMs, timestamp = DateTime.UtcNow },
                statusCode: statusCode);
        })
        .WithName("MockCustom")
        .WithSummary("Simula API customizável — passe ?statusCode=XXX&delayMs=YYY");

        mock.MapPost("/webhook-receiver", async (HttpContext ctx) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            var random = new Random();

            if (random.Next(0, 2) == 0)
            {
                return Results.Problem(
                    "Falha aleatória no webhook receiver (50% chance)",
                    statusCode: 500);
            }

            return Results.Ok(new
            {
                received = true,
                payload = body,
                receivedAt = DateTime.UtcNow
            });
        })
        .WithName("MockWebhookReceiver")
        .WithSummary("Simula receptor de webhook (falha 50% das vezes para testar Polly)");
    }
}
