using System.Collections.Generic;
using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service interface for reading and writing Orange Events data (leads, pricing matrix, past quotes) to Google Sheets.
/// </summary>
public interface IEventSheetsService
{
    /// <summary>
    /// Fetches all service pricing definitions from the Pricing Matrix sheet.
    /// </summary>
    Task<List<PricingMatrixRow>> GetPricingMatrixAsync();

    /// <summary>
    /// Fetches all historical quotation records from the Past Quotations sheet.
    /// </summary>
    Task<List<PastQuotationRow>> GetPastQuotationsAsync();

    /// <summary>
    /// Appends a new customer lead to the Leads sheet.
    /// </summary>
    Task AppendLeadAsync(LeadSheetRow lead);
}
