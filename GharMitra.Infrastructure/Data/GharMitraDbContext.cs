using GharMitra.Domain.Entities;
using GharMitra.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Data;

public class GharMitraDbContext : DbContext
{
    public GharMitraDbContext(DbContextOptions<GharMitraDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Income> Incomes => Set<Income>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GharMitraDbContext).Assembly);
    }
}