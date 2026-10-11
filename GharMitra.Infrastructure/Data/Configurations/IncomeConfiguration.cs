using GharMitra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GharMitra.Infrastructure.Data.Configurations;

public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SourceName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.IncomeDate)
            .IsRequired();

        builder.Property(x => x.CategoryId)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(x => x.UpdatedAt);

        // Foreign Key relationship
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        // Essential indexes only
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_Income_CategoryId");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_Income_IsActive");

        builder.ToTable("Incomes");
    }
}