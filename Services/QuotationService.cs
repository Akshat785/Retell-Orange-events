using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service implementation of the quotation estimation engine.
/// Utilizes a "Pricing Matrix + Blended Historical Reference" approach.
/// </summary>
public class QuotationService : IQuotationService
{
    private readonly IGoogleSheetsService _sheetsService;
    private readonly ILogger<QuotationService> _logger;
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public QuotationService(
        IGoogleSheetsService sheetsService,
        ILogger<QuotationService> logger)
    {
        _sheetsService = sheetsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuoteEstimateResponse> GetEstimateAsync(QuoteRequest request)
    {
        _logger.LogInformation("Processing quotation estimate for {Name} | Event: {Type}, Guests: {Guests}, Location: {Location}", 
            request.CustomerName, request.EventType, request.GuestCount, request.Location);

        if (request.GuestCount <= 0)
        {
            _logger.LogWarning("Invalid guest count received ({Guests}). Using a default guest count of 100 for calculations.", request.GuestCount);
            request.GuestCount = 100;
        }

        decimal matrixEstimate = 0;
        var similarQuotes = new List<PastQuotation>();

        try
        {
            // 1. Fetch Pricing Matrix and Past Quotations concurrently from Google Sheets
            var matrixTask = _sheetsService.GetPricingMatrixAsync();
            var pastTask = _sheetsService.GetPastQuotationsAsync();

            await Task.WhenAll(matrixTask, pastTask);

            var matrixItems = matrixTask.Result;
            var pastHistory = pastTask.Result;

            // 2. Calculate Pricing Matrix Base Estimate
            matrixEstimate = CalculateBasePricingMatrix(request, matrixItems);
            _logger.LogInformation("Pricing Matrix base estimate computed: {BaseCost}", FormatCurrency(matrixEstimate));

            // 3. Locate similar past quotations
            similarQuotes = LocateHistoricalMatches(request, pastHistory);
            _logger.LogInformation("Located {Count} matching historical quotation records.", similarQuotes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Downstream failure reading Google Sheets. Applying fallback general estimation package.");
            try
            {
                var logMsg = $"Exception: {ex}\n" +
                             $"Request: Name={request.CustomerName}, Mobile={request.Mobile}, Email={request.Email}, " +
                             $"Type={request.EventType}, Date={request.EventDate}, Location={request.Location}, " +
                             $"Guests={request.GuestCount}, Requirements={string.Join(", ", request.Requirements)}\n" +
                             $"Time: {DateTime.Now}\n";
                System.IO.File.WriteAllText("c:\\Users\\rasto\\.gemini\\antigravity-ide\\scratch\\RetellIntegrationApi\\error_log.txt", logMsg);
            }
            catch {}
            // sensbile fallback calculation in case of sheet errors
            matrixEstimate = (request.GuestCount * 1500m) + 30000m;
        }

        // 4. Blend calculations
        decimal blendMidpoint;
        string formulaContext;

        if (similarQuotes.Count > 0)
        {
            var historyAvg = similarQuotes.Average(q => q.FinalQuote);
            blendMidpoint = (matrixEstimate + historyAvg) / 2m;
            
            _logger.LogInformation("Calibrated using Blended Midpoint: {Blend} (Matrix Base: {Matrix}, History Avg: {HistAvg})", 
                FormatCurrency(blendMidpoint), FormatCurrency(matrixEstimate), FormatCurrency(historyAvg));

            formulaContext = $"This estimate is calibrated around a blended average of our standard pricing matrix ({FormatCurrency(matrixEstimate)}) and matching past quotations ({FormatCurrency(historyAvg)}).";
        }
        else
        {
            blendMidpoint = matrixEstimate;
            _logger.LogInformation("No historical comparable quotes found. Using base Pricing Matrix estimate: {Estimate}", FormatCurrency(blendMidpoint));
            formulaContext = $"This estimate is calibrated using our standard pricing matrix ({FormatCurrency(matrixEstimate)}) as no historical comparable records matched.";
        }

        // 5. Generate dynamic ±10% range (Do NOT round values to nearest ₹1,000 yet)
        var minEstimate = blendMidpoint * 0.90m;
        var maxEstimate = blendMidpoint * 1.10m;

        var formattedRange = $"{FormatCurrency(minEstimate)} to {FormatCurrency(maxEstimate)}";

        _logger.LogInformation("Estimation range generated successfully: {Range}", formattedRange);

        return new QuoteEstimateResponse
        {
            MinimumEstimate = minEstimate,
            MaximumEstimate = maxEstimate,
            EstimatedRange = formattedRange,
            Message = $"{formulaContext} Estimated ranges reflect ±10% tolerances based on options specified."
        };
    }

    private decimal CalculateBasePricingMatrix(QuoteRequest request, List<PricingMatrixItem> matrix)
    {
        if (matrix == null || matrix.Count == 0)
        {
            return (request.GuestCount * 1200m) + 15000m;
        }

        decimal totalCost = 0;
        var matchedAny = false;

        var eventType = request.EventType?.Trim() ?? string.Empty;
        var location = request.Location?.Trim() ?? string.Empty;

        foreach (var req in request.Requirements)
        {
            var reqTrimmed = req?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(reqTrimmed)) continue;

            // Priority 1: Event Type + Location + Service (case-insensitive & trimmed)
            var match = matrix.FirstOrDefault(m => 
                m.EventType.Trim().Equals(eventType, StringComparison.OrdinalIgnoreCase) &&
                m.Location.Trim().Equals(location, StringComparison.OrdinalIgnoreCase) &&
                m.Service.Trim().Equals(reqTrimmed, StringComparison.OrdinalIgnoreCase));

            // Priority 2: Event Type + Service (ignore location)
            if (match == null)
            {
                match = matrix.FirstOrDefault(m => 
                    m.EventType.Trim().Equals(eventType, StringComparison.OrdinalIgnoreCase) &&
                    m.Service.Trim().Equals(reqTrimmed, StringComparison.OrdinalIgnoreCase));
            }

            // Priority 3: Service only (generic fallback)
            if (match == null)
            {
                match = matrix.FirstOrDefault(m => 
                    m.Service.Trim().Equals(reqTrimmed, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
            {
                matchedAny = true;
                if (match.PricingType.Trim().Equals("PerGuest", StringComparison.OrdinalIgnoreCase))
                {
                    totalCost += (match.UnitCost * request.GuestCount) + match.AdditionalCharges;
                }
                else
                {
                    totalCost += match.UnitCost + match.AdditionalCharges;
                }
            }
        }

        if (!matchedAny)
        {
            // Standard fallback package cost if requirements are unrecognized
            totalCost = (request.GuestCount * 1500m) + 30000m;
        }

        return totalCost;
    }

    /// <summary>
    /// Locates comparable history quotation records. 
    /// Prioritizes same-location historical matches before falling back to event type + guest count matches.
    /// </summary>
    private List<PastQuotation> LocateHistoricalMatches(QuoteRequest request, List<PastQuotation> history)
    {
        if (history == null || history.Count == 0)
        {
            return new List<PastQuotation>();
        }

        // 1. Filter candidates by Event Type and guest count within ±20%
        var candidates = history.Where(q => 
            q.EventType.Equals(request.EventType, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(q.GuestCount - request.GuestCount) <= 0.20 * request.GuestCount
        ).ToList();

        if (candidates.Count == 0)
        {
            return candidates;
        }

        // 2. Prioritize Location Matches if location is specified
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var locationMatches = candidates.Where(q => 
                q.Location.Equals(request.Location, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (locationMatches.Count > 0)
            {
                _logger.LogInformation("Prioritizing {Count} comparable historical quotations from same location '{Location}'.", locationMatches.Count, request.Location);
                return locationMatches;
            }
        }

        _logger.LogInformation("Using general event-type and guest count historical comparable quotes.");
        return candidates;
    }

    /// <summary>
    /// Formats currency decimals cleanly to Indian format standard.
    /// </summary>
    private string FormatCurrency(decimal amount)
    {
        return string.Format(IndianCulture, "₹{0:N2}", amount);
    }
}
