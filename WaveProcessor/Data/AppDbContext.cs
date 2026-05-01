using Microsoft.EntityFrameworkCore;
using WaveProcessor.Models;

namespace WaveProcessor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id).HasColumnName("id");
            entity.Property(t => t.TransactionRef).HasColumnName("transaction_ref").HasMaxLength(100);
            entity.Property(t => t.Type).HasColumnName("type").HasMaxLength(20);
            entity.Property(t => t.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(t => t.FromUserId).HasColumnName("from_user_id");
            entity.Property(t => t.ToUserId).HasColumnName("to_user_id");
            entity.Property(t => t.FromPhone).HasColumnName("from_phone").HasMaxLength(20);
            entity.Property(t => t.ToPhone).HasColumnName("to_phone").HasMaxLength(20);
            entity.Property(t => t.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
            entity.Property(t => t.Fee).HasColumnName("fee").HasColumnType("numeric(18,2)");
            entity.Property(t => t.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");
            entity.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(10);
            entity.Property(t => t.Description).HasColumnName("description");
            entity.Property(t => t.AgentId).HasColumnName("agent_id");
            entity.Property(t => t.Latitude).HasColumnName("latitude");
            entity.Property(t => t.Longitude).HasColumnName("longitude");
            entity.Property(t => t.ExtraData).HasColumnName("extra_data").HasColumnType("jsonb");
            entity.Property(t => t.CreatedAt).HasColumnName("created_at");
            entity.Property(t => t.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(t => t.TransactionRef).IsUnique();
            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => t.FromUserId);
            entity.HasIndex(t => t.ToUserId);
        });
    }
}
