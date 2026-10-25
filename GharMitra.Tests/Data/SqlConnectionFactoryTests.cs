using GharMitra.Infrastructure.Data;

namespace GharMitra.Tests.Data;

public class SqlConnectionFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingConnectionString_Throws(string? connectionString)
        => Assert.Throws<ArgumentException>(() => new SqlConnectionFactory(connectionString!));

    [Fact]
    public async Task OpenConnectionAsync_UnreachableServer_Throws()
    {
        var factory = new SqlConnectionFactory(
            "Server=127.0.0.1,1;Database=x;User Id=u;Password=p;Connect Timeout=2;TrustServerCertificate=True;");

        await Assert.ThrowsAnyAsync<Exception>(() => factory.OpenConnectionAsync());
    }
}
