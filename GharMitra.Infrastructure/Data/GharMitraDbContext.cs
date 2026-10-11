using GharMitra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Data;

public class GharMitraDbContext : DbContext
{
    public GharMitraDbContext(DbContextOptions<GharMitraDbContext> options)
        : base(options)
    {
    }

    // Consistent DbSet declarations
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GharMitraDbContext).Assembly);
    }
}