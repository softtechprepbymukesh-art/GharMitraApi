using GharMitra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GharMitra.Infrastructure.Data;

public class GharMitraDbContext : DbContext
{
    public GharMitraDbContext(
        DbContextOptions<GharMitraDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(GharMitraDbContext).Assembly);
    }
}