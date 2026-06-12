using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RetellIntegrationApi.Models;
using RetellIntegrationApi.Services;

namespace RetellIntegrationApi.Controllers;

/// <summary>
/// Controller handling Retell AI voice agent real-time custom tool functions.
/// </summary>
[ApiController]
[Route("api/retell")]
public sealed class RetellToolController : ControllerBase
{
    private readonly IQuoteEstimatorService _quoteEstimator;
    private readonly ILogger<RetellToolController> _logger;

    public RetellToolController(
        IQuoteEstimatorService quoteEstimator,
        ILogger<RetellToolController> logger)
    {
        _quoteEstimator = quoteEstimator;
        _logger = logger;
    }

    /// <summary>
    /// Real-time custom tool endpoint called by Retell AI voice agents to calculate estimates.
    /// Uses both Pricing Matrix and Past Quotations dynamically.
    /// </summary>
    /// <param name="request">Enquiry details gathered by the voice agent.</param>
    /// <response code="200">Returns calculated quotation range successfully.</response>
    /// <response code="400">Bad request if inputs are invalid.</response>
    /// <response code="401">Unauthorized if api validation fails.</response>
    [HttpPost("quote/estimate-legacy")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(QuoteResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CalculateEstimate([FromBody] QuoteRequestModel request)
    {
        if (request == null)
        {
            _logger.LogWarning("Quote request received empty body.");
            return BadRequest(new { error = "Request body is null or invalid." });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.Mobile))
        {
            _logger.LogWarning("Request validation failed: customer_name or mobile missing.");
            return BadRequest(new { error = "Customer name and mobile number are required parameters." });
        }

        try
        {
            _logger.LogInformation("Processing tool call quote estimation for {Name}...", request.CustomerName);
            var result = await _quoteEstimator.EstimateQuotationAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing quotation estimation for customer {Name}.", request.CustomerName);
            
            // Standard fallback response in case of downstream failures to keep the voice call interactive
            var fallbackId = $"lead_err_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var fallbackRange = request.GuestCount > 0 
                ? $"₹{request.GuestCount * 1200:N0} to ₹{request.GuestCount * 1800:N0}"
                : "₹1,20,000 to ₹1,80,000";

            return Ok(new QuoteResponseModel
            {
                Status = "success", // Return success to avoid voice call crash, but with fallback values
                LeadId = fallbackId,
                EstimatedRange = fallbackRange,
                CaveatMessage = "Please note this is only an estimate. Our sales team will follow up with your finalized quotation."
            });
        }
    }
}
