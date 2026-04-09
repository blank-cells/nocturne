using FluentAssertions;
using Npgsql;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests.Rls;

/// <summary>
/// Database-level assertions that Row Level Security actually enforces tenant
/// isolation against the nocturne_app role. These tests intentionally bypass
/// EF Core and use raw NpgsqlConnection so they cannot be fooled by EF query
/// filters — they assert the behavior of the database, not the ORM.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RLS integration")]
public class RlsEnforcementTests
{
    private readonly RlsTestFixture _fx;

    public RlsEnforcementTests(RlsTestFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task AppRole_WithoutTenantContext_ReturnsZeroRows()
    {
        await using var conn = await _fx.OpenAppConnectionAsync();

        var count = await ScalarLongAsync(conn, "SELECT COUNT(*) FROM entries");

        count.Should().Be(0,
            "RLS with FORCE ROW LEVEL SECURITY must return no rows when app.current_tenant_id is unset");
    }

    [Fact]
    public async Task AppRole_WithTenantContext_ReturnsOnlyThatTenant()
    {
        await using (var conn = await _fx.OpenAppConnectionAsync())
        {
            await RlsTestFixture.SetTenantAsync(conn, _fx.TenantAId);
            var countA = await ScalarLongAsync(conn, "SELECT COUNT(*) FROM entries");
            countA.Should().Be(_fx.TenantAEntryCount);

            var crossA = await ScalarLongAsync(conn,
                $"SELECT COUNT(*) FROM entries WHERE tenant_id = '{_fx.TenantBId}'");
            crossA.Should().Be(0, "tenant A must not see tenant B rows even when filtering explicitly");
        }

        await using (var conn = await _fx.OpenAppConnectionAsync())
        {
            await RlsTestFixture.SetTenantAsync(conn, _fx.TenantBId);
            var countB = await ScalarLongAsync(conn, "SELECT COUNT(*) FROM entries");
            countB.Should().Be(_fx.TenantBEntryCount);
        }
    }

    [Fact]
    public async Task AppRole_InsertForWrongTenant_ThrowsWithCheckViolation()
    {
        await using var conn = await _fx.OpenAppConnectionAsync();
        await RlsTestFixture.SetTenantAsync(conn, _fx.TenantAId);

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO entries (id, tenant_id, mills, mgdl, type, sys_created_at, sys_updated_at) " +
                "VALUES (@id, @tenant, @mills, 140, 'sgv', now(), now())";
            cmd.Parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
            cmd.Parameters.Add(new NpgsqlParameter("tenant", _fx.TenantBId));
            cmd.Parameters.Add(new NpgsqlParameter("mills", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await cmd.ExecuteNonQueryAsync();
        };

        var ex = await act.Should().ThrowAsync<PostgresException>();
        // 42501 = insufficient_privilege. Npgsql surfaces RLS WITH CHECK
        // violations with this SqlState.
        ex.Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppRole_CannotDisableRls()
    {
        await using var conn = await _fx.OpenAppConnectionAsync();

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE entries DISABLE ROW LEVEL SECURITY";
            await cmd.ExecuteNonQueryAsync();
        };

        var ex = await act.Should().ThrowAsync<PostgresException>();
        ex.Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task AppRole_HasExpectedAttributes()
    {
        await using var conn = await _fx.OpenAppConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user";
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        reader.GetBoolean(0).Should().BeFalse("nocturne_app must not be a superuser");
        reader.GetBoolean(1).Should().BeFalse("nocturne_app must not have BYPASSRLS");
    }

    [Fact]
    public async Task AppRole_CannotDropPolicy()
    {
        await using var conn = await _fx.OpenAppConnectionAsync();

        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP POLICY tenant_isolation ON entries";
            await cmd.ExecuteNonQueryAsync();
        };

        var ex = await act.Should().ThrowAsync<PostgresException>();
        ex.Which.SqlState.Should().Be("42501");
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
