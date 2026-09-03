using Microsoft.EntityFrameworkCore;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Models.Domain.Model.App.Admin;
using CmdNext.Models.Domain.Model.App.Finance;
using CmdNext.Models.Domain.Model.App.Spaces;

namespace CmdNext.Repository.Implementation
{
    public class CmdNextDbContext : DbContext
    {
        public CmdNextDbContext(DbContextOptions<CmdNextDbContext> options) : base(options)
        {
        }

        // AI Schema
        public DbSet<AiChatSession> AiChatSessions { get; set; }
        public DbSet<AiChatMessage> AiChatMessages { get; set; }
        public DbSet<AiUsageLog> AiUsageLogs { get; set; }
        public DbSet<UserAiSettings> UserAiSettings { get; set; }
        public DbSet<UserAiProvider> UserAiProviders { get; set; }

        // Identity Schema
        public DbSet<User> Users { get; set; }

        // Finance Schema
        public DbSet<Category> Categories { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseItem> ExpenseItems { get; set; }
        public DbSet<Budget> Budgets { get; set; }

        // Spaces Schema
        public DbSet<Space> Spaces { get; set; }
        public DbSet<Node> Nodes { get; set; }
        public DbSet<Entry> Entries { get; set; }
        public DbSet<Attachment> Attachments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure schemas for PostgreSQL
            modelBuilder.Entity<AiChatSession>().ToTable("AiChatSessions", "ai");
            modelBuilder.Entity<AiChatMessage>().ToTable("AiChatMessages", "ai");
            modelBuilder.Entity<AiUsageLog>().ToTable("AiUsageLogs", "ai");
            modelBuilder.Entity<UserAiSettings>().ToTable("UserAiSettings", "ai");
            modelBuilder.Entity<UserAiProvider>().ToTable("UserAiProviders", "ai");

            modelBuilder.Entity<User>().ToTable("Users", "identity");

            modelBuilder.Entity<Category>().ToTable("Categories", "finance");
            modelBuilder.Entity<Expense>().ToTable("Expenses", "finance");
            modelBuilder.Entity<ExpenseItem>().ToTable("ExpenseItems", "finance");
            modelBuilder.Entity<Budget>().ToTable("Budgets", "finance");

            modelBuilder.Entity<Space>().ToTable("Spaces", "spaces");
            modelBuilder.Entity<Node>().ToTable("Nodes", "spaces");
            modelBuilder.Entity<Entry>().ToTable("Entries", "spaces");
            modelBuilder.Entity<Attachment>().ToTable("Attachments", "spaces");

            // Configure relationships
            modelBuilder.Entity<AiChatSession>()
                .HasMany(s => s.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure indexes
            modelBuilder.Entity<AiChatSession>().HasIndex(s => s.UserId);
            // Loose reference (no FK), same convention as UserId — spaces is a separate schema.
            modelBuilder.Entity<AiChatSession>().HasIndex(s => s.SpaceId);
            modelBuilder.Entity<AiChatMessage>().HasIndex(m => m.SessionId);
            modelBuilder.Entity<AiUsageLog>().HasIndex(l => l.UserId);
            modelBuilder.Entity<UserAiSettings>().HasIndex(s => s.UserId);
            modelBuilder.Entity<UserAiProvider>().HasIndex(p => p.UserId);

            // Finance relationships
            modelBuilder.Entity<Expense>()
                .HasMany(e => e.Items)
                .WithOne(i => i.Expense)
                .HasForeignKey(i => i.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExpenseItem>()
                .HasOne(i => i.Category)
                .WithMany()
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Budget>()
                .HasOne(b => b.Category)
                .WithMany()
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Money columns: fixed precision so amounts are exact.
            modelBuilder.Entity<Expense>().Property(e => e.TotalAmount).HasColumnType("numeric(12,2)");
            modelBuilder.Entity<ExpenseItem>().Property(i => i.Quantity).HasColumnType("numeric(12,3)");
            modelBuilder.Entity<ExpenseItem>().Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
            modelBuilder.Entity<ExpenseItem>().Property(i => i.LineTotal).HasColumnType("numeric(12,2)");
            modelBuilder.Entity<Budget>().Property(b => b.MonthlyLimit).HasColumnType("numeric(12,2)");

            // Finance indexes
            modelBuilder.Entity<Category>().HasIndex(c => c.UserId);
            modelBuilder.Entity<Category>().HasIndex(c => new { c.UserId, c.Name }).IsUnique();
            modelBuilder.Entity<Expense>().HasIndex(e => e.UserId);
            modelBuilder.Entity<Expense>().HasIndex(e => new { e.UserId, e.PurchasedOn });
            modelBuilder.Entity<ExpenseItem>().HasIndex(i => i.ExpenseId);
            modelBuilder.Entity<ExpenseItem>().HasIndex(i => i.CanonicalName);
            modelBuilder.Entity<Budget>().HasIndex(b => new { b.UserId, b.CategoryId }).IsUnique();

            // Spaces relationships
            modelBuilder.Entity<Node>()
                .HasOne(n => n.Parent)
                .WithMany()
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Entry>()
                .HasOne(e => e.Space)
                .WithMany()
                .HasForeignKey(e => e.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Entry>()
                .HasOne(e => e.Node)
                .WithMany()
                .HasForeignKey(e => e.NodeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Node>()
                .HasOne(n => n.Space)
                .WithMany()
                .HasForeignKey(n => n.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.Space)
                .WithMany()
                .HasForeignKey(a => a.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.Node)
                .WithMany()
                .HasForeignKey(a => a.NodeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.Entry)
                .WithMany()
                .HasForeignKey(a => a.EntryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Tags as a native text[] column
            modelBuilder.Entity<Entry>().Property(e => e.Tags).HasColumnType("text[]");

            // Spaces indexes
            modelBuilder.Entity<Space>().HasIndex(s => s.UserId);
            modelBuilder.Entity<Space>().HasIndex(s => new { s.UserId, s.Slug }).IsUnique();

            modelBuilder.Entity<Node>().HasIndex(n => new { n.SpaceId, n.Path }).IsUnique();
            modelBuilder.Entity<Node>().HasIndex(n => n.ParentId);

            modelBuilder.Entity<Entry>().HasIndex(e => new { e.SpaceId, e.Type, e.OccurredOn });
            modelBuilder.Entity<Entry>().HasIndex(e => e.NodeId);
            modelBuilder.Entity<Entry>().HasIndex(e => e.UserId);
            modelBuilder.Entity<Entry>().HasIndex(e => e.Tags).HasMethod("gin");

            modelBuilder.Entity<Attachment>().HasIndex(a => a.SpaceId);
            modelBuilder.Entity<Attachment>().HasIndex(a => a.NodeId);
            modelBuilder.Entity<Attachment>().HasIndex(a => a.EntryId);
        }
    }
}
