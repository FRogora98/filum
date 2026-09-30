using Microsoft.EntityFrameworkCore;

namespace Filum.Agent;

/// <summary>
/// The tables of Filum's turn and memory. A host applies it to its own <see cref="DbContext"/> (next to whatever else
/// that context holds, such as accounts) and keeps the migrations; the libraries never own a context.
/// </summary>
public static class FilumModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var conversation = modelBuilder.Entity<Conversation>();
        conversation.ToTable("conversations");
        conversation.HasKey(c => c.Id);
        conversation.Property(c => c.Id).HasColumnName("id");
        conversation.Property(c => c.UserId).HasColumnName("user_id");
        conversation.Property(c => c.Title).HasColumnName("title").IsRequired();
        conversation.Property(c => c.Model).HasColumnName("model").IsRequired();
        conversation.Property(c => c.CreatedAt).HasColumnName("created_at");
        conversation.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        conversation.HasIndex(c => c.UserId);

        var message = modelBuilder.Entity<ConversationMessage>();
        message.ToTable("conversation_messages");
        message.HasKey(m => m.Sequence);
        message.Property(m => m.Sequence).HasColumnName("sequence").UseIdentityAlwaysColumn();
        message.Property(m => m.Id).HasColumnName("id");
        message.Property(m => m.ConversationId).HasColumnName("conversation_id");
        message.Property(m => m.Role).HasColumnName("role").IsRequired();
        message.Property(m => m.Content).HasColumnName("content").IsRequired();
        message.Property(m => m.ReplyTo).HasColumnName("reply_to");
        message.Property(m => m.CreatedAt).HasColumnName("created_at");
        message.Property(m => m.Model).HasColumnName("model");
        message.Property(m => m.InputTokens).HasColumnName("input_tokens");
        message.Property(m => m.OutputTokens).HasColumnName("output_tokens");
        message.Property(m => m.StepsJson).HasColumnName("steps").HasColumnType("jsonb");
        message.Property(m => m.Unverified).HasColumnName("unverified").HasDefaultValue(false);
        message.Property(m => m.ProposalJson).HasColumnName("proposal").HasColumnType("jsonb");
        message.HasIndex(m => m.Id).IsUnique();
        message.HasIndex(m => m.ReplyTo).IsUnique();
        message.HasIndex(m => m.ConversationId);
        message.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        var usage = modelBuilder.Entity<UsageRecord>();
        usage.ToTable("usage_records");
        usage.HasKey(u => u.Id);
        usage.Property(u => u.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        usage.Property(u => u.UserId).HasColumnName("user_id");
        usage.Property(u => u.ConversationId).HasColumnName("conversation_id");
        usage.Property(u => u.MessageId).HasColumnName("message_id");
        usage.Property(u => u.Model).HasColumnName("model").IsRequired();
        usage.Property(u => u.InputTokens).HasColumnName("input_tokens");
        usage.Property(u => u.OutputTokens).HasColumnName("output_tokens");
        usage.Property(u => u.CostUsd).HasColumnName("cost_usd").HasPrecision(12, 6);
        usage.Property(u => u.CreatedAt).HasColumnName("created_at");
        usage.HasIndex(u => new { u.UserId, u.CreatedAt });
        usage.HasIndex(u => u.MessageId).IsUnique();

        var file = modelBuilder.Entity<MemoryFile>();
        file.ToTable("memory_files");
        file.HasKey(f => f.Id);
        file.Property(f => f.Id).HasColumnName("id");
        file.Property(f => f.UserId).HasColumnName("user_id");
        file.Property(f => f.Path).HasColumnName("path").IsRequired();
        file.Property(f => f.Content).HasColumnName("content").IsRequired();
        file.Property(f => f.Sensitivity).HasColumnName("sensitivity").IsRequired();
        file.Property(f => f.SizeBytes).HasColumnName("size_bytes");
        file.Property(f => f.LineCount).HasColumnName("line_count");
        file.Property(f => f.Header).HasColumnName("header");
        file.Property(f => f.RowCount).HasColumnName("row_count");
        file.Property(f => f.LatestRevisionId).HasColumnName("latest_revision_id");
        file.Property(f => f.Version).HasColumnName("version").IsConcurrencyToken();
        file.Property(f => f.CreatedAt).HasColumnName("created_at");
        file.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        file.Property(f => f.DeletedAt).HasColumnName("deleted_at");
        file.HasIndex(f => f.UserId);
        // One live file per path; deleted files keep their path in history without blocking a new file there.
        file.HasIndex(f => new { f.UserId, f.Path }).IsUnique().HasFilter("deleted_at IS NULL");

        var revision = modelBuilder.Entity<MemoryRevision>();
        revision.ToTable("memory_revisions");
        revision.HasKey(r => r.Id);
        revision.Property(r => r.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        revision.Property(r => r.FileId).HasColumnName("file_id");
        revision.Property(r => r.UserId).HasColumnName("user_id");
        revision.Property(r => r.Path).HasColumnName("path").IsRequired();
        revision.Property(r => r.Content).HasColumnName("content").IsRequired();
        revision.Property(r => r.Sensitivity).HasColumnName("sensitivity").IsRequired();
        revision.Property(r => r.Deleted).HasColumnName("deleted");
        revision.Property(r => r.Operation).HasColumnName("operation").IsRequired();
        revision.Property(r => r.Author).HasColumnName("author").IsRequired();
        revision.Property(r => r.ConversationId).HasColumnName("conversation_id");
        revision.Property(r => r.MessageId).HasColumnName("message_id");
        revision.Property(r => r.UndoesRevisionId).HasColumnName("undoes_revision_id");
        revision.Property(r => r.CreatedAt).HasColumnName("created_at");
        revision.HasIndex(r => new { r.UserId, r.FileId });
        revision.HasOne<MemoryFile>()
            .WithMany()
            .HasForeignKey(r => r.FileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
