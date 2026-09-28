using System;

namespace Medistock.Application.Customers.DTOs;

public record CustomerDto(
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
    DateTime CreatedAt
);

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
