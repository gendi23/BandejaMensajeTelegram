using Bandeja.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bandeja.Infrastructure;

public sealed class InboxDbContext(DbContextOptions<InboxDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.ChatId);
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.HasMany(x => x.Messages).WithOne(x => x.Conversation).HasForeignKey(x => x.ChatId);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(4096).IsRequired();
            entity.HasIndex(x => new { x.ChatId, x.TelegramMessageId })
                .IsUnique()
                .HasFilter("\"TelegramMessageId\" IS NOT NULL");
            entity.HasIndex(x => new { x.ChatId, x.CreatedAt });
        });
    }
}
