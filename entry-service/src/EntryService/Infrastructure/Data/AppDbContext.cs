using EntryService.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntryService.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LancamentoEntity> Lancamentos => Set<LancamentoEntity>();
    public DbSet<OutboxEventEntity> OutboxEvents => Set<OutboxEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LancamentoEntity>(e =>
        {
            e.ToTable("lancamentos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(7).IsRequired();
            e.Property(x => x.Valor).HasColumnName("valor").HasPrecision(15, 2).IsRequired();
            e.Property(x => x.Descricao).HasColumnName("descricao");
            e.Property(x => x.Data).HasColumnName("data").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<OutboxEventEntity>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsRequired();
            e.Property(x => x.LancamentoId).HasColumnName("lancamento_id").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.PublishedAt).HasColumnName("published_at");

            e.HasIndex(x => x.Status).HasFilter("status = 'PENDING'");
        });
    }
}
