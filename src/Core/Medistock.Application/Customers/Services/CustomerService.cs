using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;
using Medistock.Application.Customers.DTOs;

namespace Medistock.Application.Customers.Services;

public interface ICustomerService
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

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(
        string orgId,
        string query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        return _customerRepository.SearchCustomersAsync(orgId, query, limit, cancellationToken);
    }

    public Task<CustomerDto?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return _customerRepository.GetCustomerByIdAsync(customerId, cancellationToken);
    }

    public Task<CreateCustomerResult> CreateCustomerAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        return _customerRepository.CreateCustomerAsync(command, cancellationToken);
    }
}
