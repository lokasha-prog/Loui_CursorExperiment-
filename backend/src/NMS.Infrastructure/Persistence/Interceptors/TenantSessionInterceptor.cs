using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes the current tenant to SQL Server as SESSION_CONTEXT('tenant_id') every time a connection is opened.
/// The row-level-security policy reads it, so even a bug in an EF Core query filter cannot cross tenants.
/// Setting it on every open (including to NULL) means a pooled connection can never carry a stale tenant.
/// </summary>
public sealed class TenantSessionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = Build(connection, eventData);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = Build(connection, eventData);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand Build(DbConnection connection, ConnectionEndEventData eventData)
    {
        var tenantId = (eventData.Context as NmsDbContext)?.CurrentTenantId ?? Guid.Empty;

        var command = connection.CreateCommand();
        command.CommandText = "EXEC sp_set_session_context @key = N'tenant_id', @value = @tenant, @read_only = 0;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant";
        parameter.Value = tenantId == Guid.Empty ? DBNull.Value : tenantId;
        command.Parameters.Add(parameter);
        return command;
    }
}
