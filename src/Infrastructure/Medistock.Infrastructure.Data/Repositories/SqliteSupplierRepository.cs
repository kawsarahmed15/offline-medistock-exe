using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Purchases.DTOs;
using Medistock.Domain.Purchases;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteSupplierRepository : ISupplierRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteSupplierRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SupplierDto>> GetAllSuppliersAsync(string orgId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                gstin AS Gstin,
                dl_number AS DlNumber,
                phone AS Phone,
                email AS Email,
                address AS Address,
                credit_days AS CreditDays,
                outstanding_balance AS CurrentOutstandingBalance,
                is_active AS IsActive
            FROM suppliers
            WHERE org_id = @orgId
            ORDER BY name ASC;
        ";

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { orgId }, cancellationToken: cancellationToken));

        var list = new List<SupplierDto>();
        foreach (var r in rows)
        {
            list.Add(new SupplierDto(
                Id: (string)r.Id,
                Name: (string)r.Name,
                Gstin: (string?)r.Gstin,
                DlNumber: (string?)r.DlNumber,
                Phone: (string?)r.Phone,
                Email: (string?)r.Email,
                Address: (string?)r.Address,
                CreditDays: Convert.ToInt32(r.CreditDays),
                CurrentOutstandingBalance: Convert.ToDecimal(r.CurrentOutstandingBalance),
                IsActive: Convert.ToInt32(r.IsActive) == 1
            ));
        }

        return list;
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                gstin AS Gstin,
                dl_number AS DlNumber,
                phone AS Phone,
                email AS Email,
                address AS Address,
                credit_days AS CreditDays,
                outstanding_balance AS CurrentOutstandingBalance,
                is_active AS IsActive
            FROM suppliers
            WHERE id = @supplierId;
        ";

        var r = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { supplierId }, cancellationToken: cancellationToken));

        if (r == null) return null;

        return new SupplierDto(
            Id: (string)r.Id,
            Name: (string)r.Name,
            Gstin: (string?)r.Gstin,
            DlNumber: (string?)r.DlNumber,
            Phone: (string?)r.Phone,
            Email: (string?)r.Email,
            Address: (string?)r.Address,
            CreditDays: Convert.ToInt32(r.CreditDays),
            CurrentOutstandingBalance: Convert.ToDecimal(r.CurrentOutstandingBalance),
            IsActive: Convert.ToInt32(r.IsActive) == 1
        );
    }

    public async Task<string> CreateSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO suppliers (
                id, org_id, name, gstin, dl_number, phone, email,
                address, credit_days, outstanding_balance, is_active, created_at
            ) VALUES (
                @Id, @OrgId, @Name, @Gstin, @DlNumber, @Phone, @Email,
                @Address, @CreditDays, @OutstandingBalance, @IsActive, @CreatedAt
            );
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                supplier.Id,
                supplier.OrgId,
                supplier.Name,
                supplier.Gstin,
                supplier.DlNumber,
                supplier.Phone,
                supplier.Email,
                supplier.Address,
                supplier.CreditDays,
                OutstandingBalance = (double)supplier.CurrentOutstandingBalance,
                IsActive = supplier.IsActive ? 1 : 0,
                CreatedAt = supplier.CreatedAt.ToString("o")
            },
            cancellationToken: cancellationToken));

        return supplier.Id;
    }

    public async Task UpdateOutstandingBalanceAsync(
        string supplierId,
        decimal delta,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE suppliers
            SET outstanding_balance = outstanding_balance + @delta
            WHERE id = @supplierId;
        ";

        var parameters = new { supplierId, delta = (double)delta };

        if (transaction != null)
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            return;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
