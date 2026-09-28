using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Customers.DTOs;

namespace Medistock.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(
        string orgId,
        string query,
        int limit = 30,
        CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<CreateCustomerResult> CreateCustomerAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default);
}
