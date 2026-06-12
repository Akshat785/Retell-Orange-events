using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RetellIntegrationApi.Models;
using RetellIntegrationApi.Services;

namespace RetellIntegrationApi.Controllers;

/// <summary>
/// Controller for performing quotation estimations for voice agents and other modules.
/// </summary>
[ApiController]
[Route("api/retell/quote")]
public class QuotationController : ControllerBase
{
    private readonly IQuotationService _quotationService;
    private readonly IGoogleSheetsService _sheetsService;

    public QuotationController(
        IQuotationService quotationService,
        IGoogleSheetsService sheetsService)
    {
        _quotationService = quotationService;
        _sheetsService = sheetsService;
    }

    /// <summary>
    /// Estimates a quotation based on client inquiry and request characteristics.
    /// </summary>
    /// <param name="request">Enquiry specifications including guest counts, location, and requirements.</param>
    /// <returns>An estimated quotation range.</returns>
    [HttpPost("estimate")]
    public async Task<IActionResult> Estimate([FromBody] QuoteRequest request)
    {
        var result = await _quotationService.GetEstimateAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Temporary endpoint for verifying Google Sheets data integration.
    /// </summary>
    [HttpGet("test-data")]
    public async Task<IActionResult> TestData()
    {
        var pricingMatrix = await _sheetsService.GetPricingMatrixAsync();
        var pastQuotations = await _sheetsService.GetPastQuotationsAsync();

        return Ok(new
        {
            pricingMatrixCount = pricingMatrix.Count,
            pastQuotationCount = pastQuotations.Count
        });
    }
}
