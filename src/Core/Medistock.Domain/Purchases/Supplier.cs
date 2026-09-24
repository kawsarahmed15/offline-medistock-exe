using System;
using Medistock.Domain.Common;

namespace Medistock.Domain.Purchases;

public class Supplier : Entity<string>
{
    public string OrgId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Gstin { get; private set; }
    public string? DlNumber { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public int CreditDays { get; private set; } = 30;
    public decimal CurrentOutstandingBalance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Supplier() { }

    public static Supplier Create(
        string id,
        string orgId,
        string name,
        string? gstin = null,
        string? dlNumber = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        int creditDays = 30,
        decimal openingBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name is mandatory.", nameof(name));

        return new Supplier
        {
            Id = id,
            OrgId = orgId,
            Name = name.Trim(),
            Gstin = gstin?.Trim().ToUpperInvariant(),
            DlNumber = dlNumber?.Trim(),
            Phone = phone?.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            Address = address?.Trim(),
            CreditDays = Math.Max(0, creditDays),
            CurrentOutstandingBalance = openingBalance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AdjustBalance(decimal amountDelta)
    {
        CurrentOutstandingBalance += amountDelta;
    }
}
