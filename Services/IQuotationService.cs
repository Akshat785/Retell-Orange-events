using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service contract for quotation estimation processes.
/// </summary>
public interface IQuotationService
{
    /// <summary>
    /// Processes the incoming lead/inquiry details and returns a calculated quotation range.
    /// </summary>
    /// <param name="request">The quotation enquiry parameters.</param>
    /// <returns>A QuoteEstimateResponse containing the range and context information.</returns>
    Task<QuoteEstimateResponse> GetEstimateAsync(QuoteRequest request);
}
