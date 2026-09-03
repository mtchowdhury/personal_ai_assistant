using Microsoft.EntityFrameworkCore;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Models.Domain.Model.App.Admin;
using CmdNext.Models.Domain.Model.App.Finance;

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

            // Configure relationships
            modelBuilder.Entity<AiChatSession>()
                .HasMany(s => s.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure indexes
            modelBuilder.Entity<AiChatSession>().HasIndex(s => s.UserId);
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
        }
    }
}
