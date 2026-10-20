using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NMS.Infrastructure.Security;
using NMS.Infrastructure.Services;

namespace NMS.Infrastructure.Persistence;

/// <summary>Used only by the EF Core command-line tools. The connection string comes from the NMS_CONNECTION_STRING variable.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NmsDbContext>
{
    public NmsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("NMS_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set the NMS_CONNECTION_STRING environment variable before running EF Core tools.");

        var options = new DbContextOptionsBuilder<NmsDbContext>().UseSqlServer(connectionString).Options;
        return new NmsDbContext(options, new TenantContext(), new EncryptionService(RandomNumberGenerator.GetBytes(32)));
    }
}
