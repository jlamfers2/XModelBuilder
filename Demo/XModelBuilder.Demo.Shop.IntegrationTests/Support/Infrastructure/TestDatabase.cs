using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using XModelBuilder.Demo.Shop.Data;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

/// <summary>
/// Owns the SQL Server LocalDB test database and, crucially, ONE shared <see cref="SqlConnection"/>
/// that stays open for the whole run. The connection is handed to every <c>ShopDbContext</c> (test
/// code and in-process API alike), so a single per-scenario transaction can wrap all of it and be
/// rolled back afterwards - resetting the store to the committed seed without touching the schema.
/// A single physical connection also means no second connection ever enlists, so the transaction is
/// never promoted to MSDTC (which LocalDB does not support).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public string ConnectionString { get; } =
        @"Server=(localdb)\MSSQLLocalDB;Database=XModelBuilderDemoTests;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    public SqlConnection Connection { get; private set; } = null!;

    /// <summary>The transaction for the scenario currently running, or <c>null</c> outside a scenario (e.g. while seeding).</summary>
    public DbTransaction? CurrentTransaction { get; private set; }

    /// <summary>Drops and recreates the schema on a short-lived connection BEFORE the shared one opens.</summary>
    public void RecreateSchema()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(ConnectionString).Options;
        using var context = new ShopDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public void OpenSharedConnection()
    {
        Connection = new SqlConnection(ConnectionString);
        Connection.Open();
    }

    public void BeginScenarioTransaction() => CurrentTransaction = Connection.BeginTransaction();

    public void RollbackScenarioTransaction()
    {
        CurrentTransaction?.Rollback();
        CurrentTransaction?.Dispose();
        CurrentTransaction = null;
    }

    public void Dispose()
    {
        RollbackScenarioTransaction();
        Connection?.Dispose();
    }
}
