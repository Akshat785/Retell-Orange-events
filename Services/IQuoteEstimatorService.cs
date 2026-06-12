using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service interface for processing event details and generating estimated quotation ranges.
/// </summary>
public interface IQuoteEstimatorService
{
    /// <summary>
    /// Processes event details, searches pricing configuration and history, and calculates a calibrated range.
    /// Also logs the enquiry automatically as a lead.
    /// </summary>
    Task<QuoteResponseModel> EstimateQuotationAsync(QuoteRequestModel request);
}
