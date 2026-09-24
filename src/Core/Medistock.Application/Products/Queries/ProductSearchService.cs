using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Medistock.Application.Common.Interfaces;

namespace Medistock.Application.Products.Queries;

public interface IProductSearchService
{
    Task<IReadOnlyList<ProductSearchDto>> SearchAsync(
        string query,
        string warehouseId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<BarcodeLookupDto?> ScanBarcodeAsync(
        string barcode,
        string warehouseId,
        CancellationToken cancellationToken = default);
}

public class ProductSearchService : IProductSearchService
{
    private readonly IProductSearchRepository _searchRepository;

    public ProductSearchService(IProductSearchRepository searchRepository)
    {
        _searchRepository = searchRepository;
    }

    public async Task<IReadOnlyList<ProductSearchDto>> SearchAsync(
        string query,
        string warehouseId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<ProductSearchDto>();
        }

        return await _searchRepository.SearchProductsAsync(query.Trim(), warehouseId, limit, cancellationToken);
    }

    public async Task<BarcodeLookupDto?> ScanBarcodeAsync(
        string barcode,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        return await _searchRepository.LookupByBarcodeAsync(barcode.Trim(), warehouseId, cancellationToken);
    }
}
