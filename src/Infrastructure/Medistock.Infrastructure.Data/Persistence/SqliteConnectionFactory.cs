using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace Medistock.Infrastructure.Data.Persistence;

public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = MedistockPaths.Database;
        }

        // Ensure directory exists (MedistockPaths.EnsureAllDirectoriesExist covers this at startup,
        // but guard here for test scenarios that construct this directly)
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default
        };

        _connectionString = builder.ToString();
    }

    public DbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ApplyPragmas(connection);
        return connection;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ApplyPragmasAsync(connection, cancellationToken);
        return connection;
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }
    }
}
