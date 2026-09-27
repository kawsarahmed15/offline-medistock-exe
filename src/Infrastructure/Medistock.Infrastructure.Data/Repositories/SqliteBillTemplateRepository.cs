using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Medistock.Application.Common.Interfaces;
using Medistock.Contracts.Printing;

namespace Medistock.Infrastructure.Data.Repositories;

public class SqliteBillTemplateRepository : IBillTemplateRepository, IPrinterConfigRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteBillTemplateRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureSeededAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        var presets = BillTemplatePresets.GetAllPresets();
        foreach (var p in presets)
        {
            var existingIsDefault = await connection.ExecuteScalarAsync<int?>("SELECT is_default FROM bill_templates WHERE id = @Id", new { p.Id });
            var isDefault = existingIsDefault.HasValue ? (existingIsDefault.Value == 1) : p.IsDefault;
            var json = BillTemplateJsonSerializer.Serialize(p);

            if (!existingIsDefault.HasValue)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO bill_templates (id, name, is_default, paper_size, config_json, created_at, updated_at)
                    VALUES (@Id, @Name, @IsDefault, @PaperSize, @ConfigJson, @CreatedAt, @UpdatedAt)",
                    new
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsDefault = isDefault ? 1 : 0,
                        PaperSize = p.PaperSize.ToString(),
                        ConfigJson = json,
                        CreatedAt = DateTime.UtcNow.ToString("o"),
                        UpdatedAt = DateTime.UtcNow.ToString("o")
                    });
            }
            else
            {
                // Refresh preset JSON to clear out deprecated placeholder bank details or savings text
                await connection.ExecuteAsync(@"
                    UPDATE bill_templates 
                    SET name = @Name, paper_size = @PaperSize, config_json = @ConfigJson, updated_at = @UpdatedAt
                    WHERE id = @Id",
                    new
                    {
                        Id = p.Id,
                        Name = p.Name,
                        PaperSize = p.PaperSize.ToString(),
                        ConfigJson = json,
                        UpdatedAt = DateTime.UtcNow.ToString("o")
                    });
            }
        }
    }

    public async Task<BillTemplateConfig> GetDefaultTemplateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT config_json FROM bill_templates WHERE is_default = 1 LIMIT 1");

        if (!string.IsNullOrEmpty(row))
        {
            var config = BillTemplateJsonSerializer.Deserialize(row);
            if (config != null) return config;
        }

        // Fallback to first available or A4
        var anyRow = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT config_json FROM bill_templates LIMIT 1");

        if (!string.IsNullOrEmpty(anyRow))
        {
            var config = BillTemplateJsonSerializer.Deserialize(anyRow);
            if (config != null) return config;
        }

        return BillTemplatePresets.CreateA4StandardPreset();
    }

    public async Task<BillTemplateConfig?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT config_json FROM bill_templates WHERE id = @id", new { id });

        return string.IsNullOrEmpty(row) ? null : BillTemplateJsonSerializer.Deserialize(row);
    }

    public async Task<List<BillTemplateConfig>> ListAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);

        var rows = await connection.QueryAsync<string>("SELECT config_json FROM bill_templates ORDER BY is_default DESC, name ASC");
        var list = new List<BillTemplateConfig>();
        foreach (var r in rows)
        {
            var item = BillTemplateJsonSerializer.Deserialize(r);
            if (item != null) list.Add(item);
        }
        return list;
    }

    public async Task SaveTemplateAsync(BillTemplateConfig template, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);

        var json = BillTemplateJsonSerializer.Serialize(template);

        if (template.IsDefault)
        {
            await connection.ExecuteAsync("UPDATE bill_templates SET is_default = 0");
        }

        await connection.ExecuteAsync(@"
            INSERT INTO bill_templates (id, name, is_default, paper_size, config_json, created_at, updated_at)
            VALUES (@Id, @Name, @IsDefault, @PaperSize, @ConfigJson, @CreatedAt, @UpdatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                is_default = excluded.is_default,
                paper_size = excluded.paper_size,
                config_json = excluded.config_json,
                updated_at = excluded.updated_at",
            new
            {
                Id = template.Id,
                Name = template.Name,
                IsDefault = template.IsDefault ? 1 : 0,
                PaperSize = template.PaperSize.ToString(),
                ConfigJson = json,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task SetDefaultTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync("UPDATE bill_templates SET is_default = 0", transaction: tx);
        await connection.ExecuteAsync("UPDATE bill_templates SET is_default = 1 WHERE id = @id", new { id }, transaction: tx);
        tx.Commit();
    }

    public async Task DeleteTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM bill_templates WHERE id = @id", new { id });
    }

    // Printer Configurations
    public async Task<List<PrinterConfigurationDto>> ListPrinterConfigsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);
        var rows = await connection.QueryAsync<dynamic>("SELECT * FROM printer_configurations ORDER BY name ASC");
        return rows.Select(r => new PrinterConfigurationDto
        {
            Id = (string)r.id,
            Name = (string)r.name,
            InterfaceType = Enum.Parse<PrinterInterfaceType>((string)r.interface_type),
            TargetNameOrIp = (string)r.target_name_or_ip,
            TargetPort = (int)r.target_port,
            PaperSize = Enum.Parse<PaperSize>((string)r.paper_size),
            AssignedTemplateId = (string?)r.assigned_template_id,
            AutoCutPaper = ((long)r.auto_cut_paper) == 1,
            KickCashDrawer = ((long)r.kick_cash_drawer) == 1,
            IsDefaultPos = ((long)r.is_default_pos) == 1,
            IsDefaultA4 = ((long)r.is_default_a4) == 1
        }).ToList();
    }

    public async Task<PrinterConfigurationDto?> GetDefaultPosPrinterAsync(CancellationToken cancellationToken = default)
    {
        var all = await ListPrinterConfigsAsync(cancellationToken);
        return all.FirstOrDefault(p => p.IsDefaultPos) ?? all.FirstOrDefault();
    }

    public async Task<PrinterConfigurationDto?> GetDefaultA4PrinterAsync(CancellationToken cancellationToken = default)
    {
        var all = await ListPrinterConfigsAsync(cancellationToken);
        return all.FirstOrDefault(p => p.IsDefaultA4) ?? all.FirstOrDefault();
    }

    public async Task SavePrinterConfigAsync(PrinterConfigurationDto config, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await EnsureSeededAsync(connection, cancellationToken);
        if (config.IsDefaultPos)
            await connection.ExecuteAsync("UPDATE printer_configurations SET is_default_pos = 0");
        if (config.IsDefaultA4)
            await connection.ExecuteAsync("UPDATE printer_configurations SET is_default_a4 = 0");

        if (string.IsNullOrEmpty(config.Id)) config.Id = Guid.NewGuid().ToString();

        await connection.ExecuteAsync(@"
            INSERT INTO printer_configurations (id, name, interface_type, target_name_or_ip, target_port, paper_size, assigned_template_id, auto_cut_paper, kick_cash_drawer, is_default_pos, is_default_a4, created_at, updated_at)
            VALUES (@Id, @Name, @InterfaceType, @TargetNameOrIp, @TargetPort, @PaperSize, @AssignedTemplateId, @AutoCutPaper, @KickCashDrawer, @IsDefaultPos, @IsDefaultA4, @CreatedAt, @UpdatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                interface_type = excluded.interface_type,
                target_name_or_ip = excluded.target_name_or_ip,
                target_port = excluded.target_port,
                paper_size = excluded.paper_size,
                assigned_template_id = excluded.assigned_template_id,
                auto_cut_paper = excluded.auto_cut_paper,
                kick_cash_drawer = excluded.kick_cash_drawer,
                is_default_pos = excluded.is_default_pos,
                is_default_a4 = excluded.is_default_a4,
                updated_at = excluded.updated_at",
            new
            {
                Id = config.Id,
                Name = config.Name,
                InterfaceType = config.InterfaceType.ToString(),
                TargetNameOrIp = config.TargetNameOrIp,
                TargetPort = config.TargetPort,
                PaperSize = config.PaperSize.ToString(),
                AssignedTemplateId = config.AssignedTemplateId,
                AutoCutPaper = config.AutoCutPaper ? 1 : 0,
                KickCashDrawer = config.KickCashDrawer ? 1 : 0,
                IsDefaultPos = config.IsDefaultPos ? 1 : 0,
                IsDefaultA4 = config.IsDefaultA4 ? 1 : 0,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            });
    }

    public async Task DeletePrinterConfigAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM printer_configurations WHERE id = @id", new { id });
    }
}
