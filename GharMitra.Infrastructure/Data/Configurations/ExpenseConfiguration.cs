using GharMitra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GharMitra.Infrastructure.Data.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ExpenseDate)
            .IsRequired();

        builder.Property(x => x.ExpenseType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PaymentMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SpendingType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Foreign Keys
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.Family)
            .WithMany()
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.FamilyMember)
            .WithMany()
            .HasForeignKey(x => x.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_Expense_CategoryId");

        builder.HasIndex(x => x.FamilyId)
            .HasDatabaseName("IX_Expense_FamilyId");

        builder.HasIndex(x => x.FamilyMemberId)
            .HasDatabaseName("IX_Expense_FamilyMemberId");

        builder.ToTable("Expenses");
    }
}