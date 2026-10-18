// ============================================================
// AppDbContext.cs — Contexto principal do Entity Framework Core
//
// O que é o DbContext?
// É a "ponte" entre as entidades C# e as tabelas do banco de dados.
// O EF Core usa esta classe para:
// - Criar/alterar tabelas (via migrations)
// - Converter queries LINQ em SQL
// - Rastrear mudanças nas entidades (change tracking)
// ============================================================
using ApiReliabilitySuite.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MonitorEntity = ApiReliabilitySuite.Domain.Entities.Monitor;

namespace ApiReliabilitySuite.Infrastructure.Data;

public class AppDbContext : DbContext
{
    // Construtor: recebe as opções (connection string, provider) via DI
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSet = tabela no banco. O EF Core cria Monitor → tabela "Monitors"
    public DbSet<MonitorEntity> Monitors { get; set; }
    public DbSet<HealthCheckResult> HealthCheckResults { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }

    /// <summary>
    /// Configura mapeamento detalhado: restrições, índices, nomes de colunas.
    /// É chamado pelo EF Core durante as migrations e queries.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Configuração: Monitor ──────────────────────────────────────
        modelBuilder.Entity<MonitorEntity>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Url).IsRequired().HasMaxLength(500);

            // Armazena enum como string ("Up" em vez de "1") — mais legível no banco
            entity.Property(m => m.Status).HasConversion<string>();

            // Índice para busca rápida de monitores ativos (usado pelo Worker)
            entity.HasIndex(m => m.IsActive);
        });

        // ── Configuração: HealthCheckResult ───────────────────────────
        modelBuilder.Entity<HealthCheckResult>(entity =>
        {
            entity.HasKey(h => h.Id);

            // Relacionamento: HealthCheckResult pertence a um Monitor
            // OnDelete CASCADE: se o Monitor for deletado, os resultados também são
            entity.HasOne(h => h.Monitor)
                  .WithMany(m => m.Results)
                  .HasForeignKey(h => h.MonitorId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Índice composto para busca eficiente por monitor + data
            entity.HasIndex(h => new { h.MonitorId, h.CheckedAt });
        });

        // ── Configuração: WebhookDelivery ──────────────────────────────
        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.TargetUrl).IsRequired().HasMaxLength(500);

            // Status como string no banco (legível)
            entity.Property(w => w.Status).HasConversion<string>();

            // Índice no Status: o Worker busca webhooks "Failed" frequentemente
            entity.HasIndex(w => w.Status);
        });
    }
}
