using System.Collections.Generic;
using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service interface for interacting with the Google Sheets API.
/// </summary>
public interface IGoogleSheetsService
{
    /// <summary>
    /// Appends a new call webhook record row into the configured Google Sheet.
    /// </summary>
    /// <param name="row">The structured call details to save.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task AppendRowAsync(GoogleSheetRow row);

    /// <summary>
    /// Fetches all service pricing definitions from the Pricing Matrix sheet.
    /// </summary>
    Task<List<PricingMatrixItem>> GetPricingMatrixAsync();

    /// <summary>
    /// Fetches all historical quotation records from the Past Quotations sheet.
    /// </summary>
    Task<List<PastQuotation>> GetPastQuotationsAsync();

    /// <summary>
    /// Dynamically creates a new Lead in the Google Sheets CRM.
    /// Generates a sequential LEAD-XXXXXX ID by locating the highest existing LeadId in the sheet.
    /// </summary>
    Task<string> CreateLeadAsync(CreateLeadRequest request);

    /// <summary>
    /// Updates the quote range and changes status to "Quoted" for a lead row in the CRM.
    /// </summary>
    Task UpdateLeadEstimateAsync(UpdateLeadEstimateRequest request);
}
