using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace QuraEx.BuildingBlocks.Persistence;

/// <summary>Applies all QuraEx DB conventions from docs/database/conventions.md.
///
/// Usage in each service DbContext:
///   protected override void OnModelCreating(ModelBuilder b) => b.ApplyQuraExConventions();
///   protected override void ConfigureConventions(ModelConfigurationBuilder c) => c.ApplyQuraExConventions().
/// </summary>
public static class EfConventions
{
    /// <summary>Called from OnModelCreating — applies per-entity conventions (soft-delete filter, xmin).</summary>
    /// <returns></returns>
    public static ModelBuilder ApplyQuraExConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Soft-delete global query filter — ONLY entities implementing ISoftDeletable.
            // OutboxMessage/ProcessedMessage/*Snapshot must NOT implement ISoftDeletable
            // so the relay query "WHERE processed_at IS NULL" has no spurious deleted_at predicate.
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var applyMethod = typeof(EfConventions)
                    .GetMethod(nameof(ApplySoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(clrType);
                applyMethod.Invoke(null, [modelBuilder]);
            }
        }

        return modelBuilder;
    }

    /// <summary>Called from ConfigureConventions — applies solution-wide type conventions (enum→string, xmin).
    /// Enum-as-varchar: readable in DB (WHERE status='DRAFT'), no Postgres native enum migration pain.
    /// xmin: zero-extra-column concurrency token, applied via Npgsql UseXminAsConcurrencyToken() in each entity config.</summary>
    /// <returns></returns>
    public static ModelConfigurationBuilder ApplyQuraExConventions(this ModelConfigurationBuilder configurationBuilder)
    {
        // Enums stored as varchar — applies to ALL enum properties solution-wide.
        // Per convention: HasConversion<string>() means enum values are stored as their .ToString() names.
        configurationBuilder
            .Properties<Enum>()
            .HaveConversion<string>();

        return configurationBuilder;
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }
}

/// <summary>Fluent helper to declare a partial unique index (WHERE deleted_at IS NULL).
/// Must be called explicitly per entity — the convention helper cannot infer which columns are unique.
/// See conventions.md: "Unique columns MUST use partial unique index WHERE deleted_at IS NULL.".</summary>
public static class EntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<TEntity> HasPartialUniqueIndex<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string columnName,
        string? indexName = null)
        where TEntity : class
    {
        builder.HasIndex(columnName)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName(indexName ?? $"ix_{typeof(TEntity).Name.ToLowerInvariant()}_{columnName}_unique_not_deleted");

        return builder;
    }
}
