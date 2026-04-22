using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OmniX.Infrastructure.Data;

// ════════════════════════════════════════════════════════════════════════════
//  TenantRLSInterceptor — من Ultra v6
//  يضبط app.current_tenant_id على كل connection لـ PostgreSQL RLS
//  يعمل على مستوى قاعدة البيانات نفسها — أقوى من Query Filter وحده
// ════════════════════════════════════════════════════════════════════════════
public class TenantRLSInterceptor : DbConnectionInterceptor
{
    private readonly ITenantProvider _tenantProvider;

    public TenantRLSInterceptor(ITenantProvider tenantProvider)
        => _tenantProvider = tenantProvider;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantId(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection,
        ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetTenantIdAsync(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetTenantId(DbConnection connection)
    {
        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue) return;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant_id = '{tenantId.Value}';";
        cmd.ExecuteNonQuery();
    }

    private async Task SetTenantIdAsync(DbConnection connection)
    {
        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue) return;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET app.current_tenant_id = '{tenantId.Value}';";
        await cmd.ExecuteNonQueryAsync();
    }
}

public interface ITenantProvider
{
    Guid? TenantId { get; }
    void SetTenantId(Guid tenantId);
}
