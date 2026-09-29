using System;

namespace Medistock.Application.Customers.DTOs;

public record CustomerDto
{
    public string Id { get; init; } = string.Empty;
    public string OrgId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Pincode { get; init; }
    public string? Gstin { get; init; }
    public string? DlNumber { get; init; }
    public decimal CreditLimit { get; init; }
    public decimal CurrentBalance { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public CustomerDto() { }

    public CustomerDto(
        string Id,
        string OrgId,
        string Name,
        string? Phone,
        string? Address,
        string? City,
        string? State,
        string? Pincode,
        string? Gstin,
        string? DlNumber,
        decimal CreditLimit,
        decimal CurrentBalance,
        bool IsActive,
        DateTime CreatedAt)
    {
        this.Id = Id;
        this.OrgId = OrgId;
        this.Name = Name;
        this.Phone = Phone;
        this.Address = Address;
        this.City = City;
        this.State = State;
        this.Pincode = Pincode;
        this.Gstin = Gstin;
        this.DlNumber = DlNumber;
        this.CreditLimit = CreditLimit;
        this.CurrentBalance = CurrentBalance;
        this.IsActive = IsActive;
        this.CreatedAt = CreatedAt;
    }
}

public record CreateCustomerCommand(
    string OrgId,
    string Name,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? Pincode,
    string? Gstin,
    string? DlNumber,
    decimal CreditLimit,
    decimal OpeningBalance
);

public record CreateCustomerResult(
    bool Success,
    string? CustomerId,
    CustomerDto? Customer,
    string? ErrorMessage
);
