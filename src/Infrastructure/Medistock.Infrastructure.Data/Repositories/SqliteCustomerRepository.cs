using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Customers.DTOs;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteCustomerRepository : ICustomerRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteCustomerRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(
        string orgId,
        string query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        string sql;
        object parameters;

        if (string.IsNullOrWhiteSpace(query))
        {
            sql = @"
                SELECT 
                    id AS Id,
                    org_id AS OrgId,
                    name AS Name,
                    phone AS Phone,
                    address AS Address,
                    city AS City,
                    state AS State,
                    pincode AS Pincode,
                    gstin AS Gstin,
                    dl_number AS DlNumber,
                    credit_limit AS CreditLimit,
                    current_balance AS CurrentBalance,
                    is_active AS IsActive,
                    created_at AS CreatedAt
                FROM customers
                WHERE org_id = @orgId AND is_active = 1
                ORDER BY name ASC
                LIMIT @limit;
            ";
            parameters = new { orgId, limit };
        }
        else
        {
            var pattern = $"%{query.Trim()}%";
            sql = @"
                SELECT 
                    id AS Id,
                    org_id AS OrgId,
                    name AS Name,
                    phone AS Phone,
                    address AS Address,
                    city AS City,
                    state AS State,
                    pincode AS Pincode,
                    gstin AS Gstin,
                    dl_number AS DlNumber,
                    credit_limit AS CreditLimit,
                    current_balance AS CurrentBalance,
                    is_active AS IsActive,
                    created_at AS CreatedAt
                FROM customers
                WHERE org_id = @orgId 
                  AND is_active = 1
                  AND (name LIKE @pattern OR phone LIKE @pattern OR city LIKE @pattern OR address LIKE @pattern)
                ORDER BY 
                    CASE 
                        WHEN name LIKE @prefix THEN 1
                        WHEN phone LIKE @prefix THEN 2
                        ELSE 3
                    END,
                    name ASC
                LIMIT @limit;
            ";
            parameters = new { orgId, pattern, prefix = $"{query.Trim()}%", limit };
        }

        var results = await conn.QueryAsync<CustomerDto>(sql, parameters);
        return results.ToList();
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                id AS Id,
                org_id AS OrgId,
                name AS Name,
                phone AS Phone,
                address AS Address,
                city AS City,
                state AS State,
                pincode AS Pincode,
                gstin AS Gstin,
                dl_number AS DlNumber,
                credit_limit AS CreditLimit,
                current_balance AS CurrentBalance,
                is_active AS IsActive,
                created_at AS CreatedAt
            FROM customers
            WHERE id = @customerId;
        ";

        return await conn.QuerySingleOrDefaultAsync<CustomerDto>(sql, new { customerId });
    }

    public async Task<CreateCustomerResult> CreateCustomerAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new CreateCustomerResult(false, null, null, "Party Name is required.");
        }

        var customerId = Ulid.NewUlid().ToString();
        var now = DateTime.UtcNow;

        using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO customers (
                id, org_id, name, phone, address, city, state, pincode,
                gstin, dl_number, credit_limit, current_balance, is_active,
                created_at, updated_at
            ) VALUES (
                @Id, @OrgId, @Name, @Phone, @Address, @City, @State, @Pincode,
                @Gstin, @DlNumber, @CreditLimit, @CurrentBalance, 1,
                @CreatedAt, @UpdatedAt
            );
        ";

        await conn.ExecuteAsync(sql, new
        {
            Id = customerId,
            OrgId = command.OrgId,
            Name = command.Name.Trim().ToUpperInvariant(),
            Phone = string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(command.Address) ? null : command.Address.Trim(),
            City = string.IsNullOrWhiteSpace(command.City) ? null : command.City.Trim(),
            State = string.IsNullOrWhiteSpace(command.State) ? null : command.State.Trim(),
            Pincode = string.IsNullOrWhiteSpace(command.Pincode) ? null : command.Pincode.Trim(),
            Gstin = string.IsNullOrWhiteSpace(command.Gstin) ? null : command.Gstin.Trim().ToUpperInvariant(),
            DlNumber = string.IsNullOrWhiteSpace(command.DlNumber) ? null : command.DlNumber.Trim().ToUpperInvariant(),
            CreditLimit = (double)command.CreditLimit,
            CurrentBalance = (double)command.OpeningBalance,
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o")
        });

        var created = new CustomerDto(
            Id: customerId,
            OrgId: command.OrgId,
            Name: command.Name.Trim().ToUpperInvariant(),
            Phone: string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim(),
            Address: string.IsNullOrWhiteSpace(command.Address) ? null : command.Address.Trim(),
            City: string.IsNullOrWhiteSpace(command.City) ? null : command.City.Trim(),
            State: string.IsNullOrWhiteSpace(command.State) ? null : command.State.Trim(),
            Pincode: string.IsNullOrWhiteSpace(command.Pincode) ? null : command.Pincode.Trim(),
            Gstin: string.IsNullOrWhiteSpace(command.Gstin) ? null : command.Gstin.Trim().ToUpperInvariant(),
            DlNumber: string.IsNullOrWhiteSpace(command.DlNumber) ? null : command.DlNumber.Trim().ToUpperInvariant(),
            CreditLimit: command.CreditLimit,
            CurrentBalance: command.OpeningBalance,
            IsActive: true,
            CreatedAt: now
        );

        return new CreateCustomerResult(true, customerId, created, null);
    }
}
