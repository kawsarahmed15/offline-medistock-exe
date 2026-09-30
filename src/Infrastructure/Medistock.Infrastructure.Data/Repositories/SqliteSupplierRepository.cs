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
                s.id AS Id,
                s.name AS Name,
                s.gstin AS Gstin,
                s.dl_number AS DlNumber,
                s.phone AS Phone,
                s.email AS Email,
                s.address AS Address,
                s.credit_days AS CreditDays,
                s.outstanding_balance AS CurrentOutstandingBalance,
                s.is_active AS IsActive,
                (
                    SELECT GROUP_CONCAT('PO-' || PRINTF('%04d', (
                        SELECT COUNT(1) 
                        FROM purchase_invoices pi2 
                        WHERE pi2.org_id = pi.org_id 
                          AND (pi2.created_at < pi.created_at OR (pi2.created_at = pi.created_at AND pi2.rowid <= pi.rowid))
                    )), ', ')
                    FROM purchase_invoices pi
                    WHERE (pi.supplier_id = s.id OR (pi.supplier_name IS NOT NULL AND pi.supplier_name = s.name))
                      AND pi.status != 2
                ) AS PurchaseNo,
                (
                    SELECT GROUP_CONCAT(pi.supplier_invoice_no, ', ')
                    FROM purchase_invoices pi
                    WHERE (pi.supplier_id = s.id OR (pi.supplier_name IS NOT NULL AND pi.supplier_name = s.name))
                      AND pi.status != 2
                ) AS InvoiceNo
            FROM suppliers s
            WHERE s.org_id = @orgId
            ORDER BY s.name ASC;
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
                IsActive: Convert.ToInt32(r.IsActive) == 1,
                PurchaseNo: (string?)r.PurchaseNo,
                InvoiceNo: (string?)r.InvoiceNo
            ));
        }

        return list;
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(string supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                s.id AS Id,
                s.name AS Name,
                s.gstin AS Gstin,
                s.dl_number AS DlNumber,
                s.phone AS Phone,
                s.email AS Email,
                s.address AS Address,
                s.credit_days AS CreditDays,
                s.outstanding_balance AS CurrentOutstandingBalance,
                s.is_active AS IsActive,
                (
                    SELECT GROUP_CONCAT('PO-' || PRINTF('%04d', (
                        SELECT COUNT(1) 
                        FROM purchase_invoices pi2 
                        WHERE pi2.org_id = pi.org_id 
                          AND (pi2.created_at < pi.created_at OR (pi2.created_at = pi.created_at AND pi2.rowid <= pi.rowid))
                    )), ', ')
                    FROM purchase_invoices pi
                    WHERE (pi.supplier_id = s.id OR (pi.supplier_name IS NOT NULL AND pi.supplier_name = s.name))
                      AND pi.status != 2
                ) AS PurchaseNo,
                (
                    SELECT GROUP_CONCAT(pi.supplier_invoice_no, ', ')
                    FROM purchase_invoices pi
                    WHERE (pi.supplier_id = s.id OR (pi.supplier_name IS NOT NULL AND pi.supplier_name = s.name))
                      AND pi.status != 2
                ) AS InvoiceNo
            FROM suppliers s
            WHERE s.id = @supplierId;
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
            IsActive: Convert.ToInt32(r.IsActive) == 1,
            PurchaseNo: (string?)r.PurchaseNo,
            InvoiceNo: (string?)r.InvoiceNo
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
