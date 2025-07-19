// Analytics.Data/Context/AnalyticsDbContext.cs
using Analytics.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Analytics.Data.Context
{
    public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
    {
        // Removed the static SqliteConnection here.
        // It's better to let the consumer (like the test fixture) manage the connection
        // for "pristine on every run" behavior.

        public DbSet<WebAnalyticsEvent> WebAnalyticsEvents { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WebAnalyticsEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventType).IsRequired().HasMaxLength(255);
                entity.Property(e => e.UserId).HasMaxLength(255);
                entity.Property(e => e.SessionId).HasMaxLength(255);
                entity.Property(e => e.PageUrl).HasMaxLength(2048);
                entity.Property(e => e.ReferrerUrl).HasMaxLength(2048);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.DataPayload).HasColumnType("TEXT");

                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.EventType);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.SessionId);
            });

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Creates DbContextOptions for an in-memory SQLite database, ensuring it's pristine on each run.
        /// Useful for testing and development where persistence is not desired.
        /// </summary>
        /// <param name="databaseName">
        /// An optional unique name for the in-memory database instance.
        /// If provided, the database will be shared across connections using the same name and "Cache=Shared".
        /// If null or empty, a truly unique, non-shared in-memory database will be created.
        /// </param>
        /// <returns>DbContextOptions configured for in-memory SQLite.</returns>
        public static DbContextOptions<AnalyticsDbContext> CreateInMemorySqliteOptions(string? databaseName = null)
        {
            var connectionString = string.IsNullOrEmpty(databaseName)
                ? "DataSource=:memory:" // Completely new, non-shared in-memory DB per call
                : $"DataSource={databaseName};Mode=Memory;Cache=Shared"; // Shared in-memory DB by name

            // Crucially, create a NEW SqliteConnection for each call to this method.
            // This allows the consumer (the test fixture) to manage its lifecycle.
            var connection = new SqliteConnection(connectionString);
            connection.Open(); // Open the connection immediately

            return new DbContextOptionsBuilder<AnalyticsDbContext>()
                .UseSqlite(connection)
                .Options;
        }
    }
}