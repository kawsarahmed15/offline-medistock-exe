using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Inventory.DTOs;
using Medistock.Domain.Common;
using Medistock.Domain.Compliance;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteScheduleDrugRepository : IScheduleDrugRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteScheduleDrugRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task RecordScheduleDrugEntriesAsync(
        IReadOnlyList<ScheduleDrugRegisterEntry> entries,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0) return;

        const string sql = @"
            INSERT INTO schedule_drug_register (
                id, org_id, branch_id, sale_id, invoice_no, sale_date,
                product_id, product_name, schedule, batch_number, expiry_date,
                quantity, patient_name, patient_address, patient_phone,
                doctor_name, doctor_reg_no, doctor_address, prescription_ref,
                prescription_date, dispensed_by_user_id, created_at
            ) VALUES (
                @Id, @OrgId, @BranchId, @SaleId, @InvoiceNo, @SaleDate,
                @ProductId, @ProductName, @Schedule, @BatchNumber, @ExpiryDate,
                @Quantity, @PatientName, @PatientAddress, @PatientPhone,
                @DoctorName, @DoctorRegNo, @DoctorAddress, @PrescriptionRef,
                @PrescriptionDate, @DispensedByUserId, @CreatedAt
            );
        ";

        var parameters = entries.Select(e => new
        {
            e.Id,
            e.OrgId,
            e.BranchId,
            e.SaleId,
            e.InvoiceNo,
            SaleDate = e.SaleDate.ToString("o"),
            e.ProductId,
            e.ProductName,
            Schedule = (int)e.Schedule,
            e.BatchNumber,
            ExpiryDate = e.ExpiryDate.ToString("o"),
            Quantity = (double)e.Quantity,
            e.PatientName,
            e.PatientAddress,
            e.PatientPhone,
            e.DoctorName,
            e.DoctorRegNo,
            e.DoctorAddress,
            e.PrescriptionRef,
            PrescriptionDate = e.PrescriptionDate?.ToString("o"),
            e.DispensedByUserId,
            CreatedAt = e.CreatedAt.ToString("o")
        });

        if (transaction != null)
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            return;
        }

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ScheduleDrugRegisterDto>> GetScheduleRegisterAsync(
        ScheduleDrugFilter filter,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var sb = new StringBuilder(@"
            SELECT 
                id AS Id,
                invoice_no AS InvoiceNo,
                sale_date AS SaleDateStr,
                product_name AS ProductName,
                schedule AS Schedule,
                batch_number AS BatchNumber,
                expiry_date AS ExpiryDateStr,
                quantity AS Quantity,
                patient_name AS PatientName,
                patient_phone AS PatientPhone,
                patient_address AS PatientAddress,
                doctor_name AS DoctorName,
                doctor_reg_no AS DoctorRegNo,
                prescription_ref AS PrescriptionRef,
                prescription_date AS PrescriptionDateStr,
                dispensed_by_user_id AS DispensedByUserId
            FROM schedule_drug_register
            WHERE 1=1
        ");

        var parameters = new DynamicParameters();
        parameters.Add("limit", filter.Limit > 0 ? filter.Limit : 100);

        if (filter.StartDate.HasValue)
        {
            sb.Append(" AND sale_date >= @startDate");
            parameters.Add("startDate", filter.StartDate.Value.ToString("o"));
        }

        if (filter.EndDate.HasValue)
        {
            sb.Append(" AND sale_date <= @endDate");
            parameters.Add("endDate", filter.EndDate.Value.ToString("o"));
        }

        if (filter.Schedule.HasValue)
        {
            sb.Append(" AND schedule = @schedule");
            parameters.Add("schedule", (int)filter.Schedule.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            sb.Append(@" AND (
                product_name LIKE @query OR 
                patient_name LIKE @query OR 
                patient_phone LIKE @query OR 
                doctor_name LIKE @query OR 
                invoice_no LIKE @query
            )");
            parameters.Add("query", $"%{filter.SearchQuery.Trim()}%");
        }

        sb.Append(" ORDER BY sale_date DESC LIMIT @limit;");

        var rows = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sb.ToString(), parameters, cancellationToken: cancellationToken));

        var list = new List<ScheduleDrugRegisterDto>();
        foreach (var r in rows)
        {
            var saleDate = DateTime.TryParse((string)r.SaleDateStr, out DateTime sDate) ? sDate : DateTime.UtcNow;
            var expDate = DateTime.TryParse((string)r.ExpiryDateStr, out DateTime eDate) ? eDate : DateTime.MaxValue;
            DateTime? presDate = !string.IsNullOrEmpty((string?)r.PrescriptionDateStr) && DateTime.TryParse((string)r.PrescriptionDateStr, out DateTime pDate) ? pDate : null;

            list.Add(new ScheduleDrugRegisterDto(
                Id: (string)r.Id,
                InvoiceNo: (string)r.InvoiceNo,
                SaleDate: saleDate,
                ProductName: (string)r.ProductName,
                Schedule: (DrugSchedule)(int)r.Schedule,
                BatchNumber: (string)r.BatchNumber,
                ExpiryDate: expDate,
                Quantity: Convert.ToDecimal(r.Quantity),
                PatientName: (string)r.PatientName,
                PatientPhone: (string?)r.PatientPhone,
                PatientAddress: (string?)r.PatientAddress,
                DoctorName: (string)r.DoctorName,
                DoctorRegNo: (string?)r.DoctorRegNo,
                PrescriptionRef: (string?)r.PrescriptionRef,
                PrescriptionDate: presDate,
                DispensedByUserId: (string)r.DispensedByUserId
            ));
        }

        return list;
    }
}
