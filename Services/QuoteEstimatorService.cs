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
/// Utilizes a "Pricing Matrix + Historical Quotation Reference" approach.
/// </summary>
public sealed class QuoteEstimatorService : IQuoteEstimatorService
{
    private readonly IEventSheetsService _sheetsService;
    private readonly ILogger<QuoteEstimatorService> _logger;
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public QuoteEstimatorService(
        IEventSheetsService sheetsService,
        ILogger<QuoteEstimatorService> logger)
    {
        _sheetsService = sheetsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuoteResponseModel> EstimateQuotationAsync(QuoteRequestModel request)
    {
        _logger.LogInformation("Initiating quotation estimation for event: {Type}, Guests: {Guests}, Location: {Location}", 
            request.EventType, request.GuestCount, request.Location);

        if (request.GuestCount <= 0)
        {
            request.GuestCount = 100; // Fallback sensible default
            _logger.LogWarning("Invalid guest count received. Defaulted to 100.");
        }

        decimal matrixEstimate = 0;
        var similarQuotes = new List<PastQuotationRow>();

        try
        {
            // Step 1: Read Google Sheets (Pricing Matrix & History)
            var matrixRowsTask = _sheetsService.GetPricingMatrixAsync();
            var pastQuotesTask = _sheetsService.GetPastQuotationsAsync();

            await Task.WhenAll(matrixRowsTask, pastQuotesTask);

            var matrixRows = matrixRowsTask.Result;
            var pastQuotes = pastQuotesTask.Result;

            // Step 2: Calculate Base Estimate using Pricing Matrix
            matrixEstimate = CalculateBaseMatrixCost(request, matrixRows);
            _logger.LogInformation("Pricing Matrix Base Estimate: {Cost}", FormatCurrency(matrixEstimate));

            // Step 3: Find similar quotations
            similarQuotes = LocateSimilarQuotations(request, pastQuotes);
            _logger.LogInformation("Located {Count} matching past quotation records.", similarQuotes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read data from Google Sheets. Falling back to base matrix calculation rules.");
            // Generates a sensible default base price if sheets are completely inaccessible
            matrixEstimate = request.GuestCount * 1500m + 25000m; 
        }

        // Step 4: Calibrate Estimate Range using both sources
        decimal minRange;
        decimal maxRange;

        if (similarQuotes.Count > 0)
        {
            // Calculate average of similar quotations
            var historyAvg = similarQuotes.Average(q => q.FinalQuoteAmount);
            _logger.LogInformation("Average of similar historical quotations: {Avg}", FormatCurrency(historyAvg));

            // Blended Average Midpoint
            var blend = (matrixEstimate + historyAvg) / 2m;

            // Deviation factor
            var deviation = Math.Abs(historyAvg - blend);

            // Generate range centered around historical average, bounded by the blend
            minRange = historyAvg - deviation;
            maxRange = historyAvg + deviation;

            _logger.LogInformation("Dual-source calibrated range: {Min} to {Max}", FormatCurrency(minRange), FormatCurrency(maxRange));
        }
        else
        {
            // Fallback: Pricing Matrix Estimate only (+/- 20% range)
            minRange = matrixEstimate * 0.80m;
            maxRange = matrixEstimate * 1.20m;

            _logger.LogInformation("Pricing Matrix only fallback range: {Min} to {Max}", FormatCurrency(minRange), FormatCurrency(maxRange));
        }

        // Round to nearest 1,000 for realistic, professional appearance
        minRange = Math.Round(minRange / 1000m) * 1000m;
        maxRange = Math.Round(maxRange / 1000m) * 1000m;

        var formattedRange = $"{FormatCurrency(minRange)} to {FormatCurrency(maxRange)}";
        var leadId = $"lead_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        // Step 5: Save inquiry as a Lead asynchronously
        var leadRow = new LeadSheetRow
        {
            LeadId = leadId,
            Name = request.CustomerName,
            Mobile = request.Mobile,
            Email = request.Email,
            EventType = request.EventType,
            EventDate = request.EventDate,
            Location = request.Location,
            GuestCount = request.GuestCount,
            Requirements = string.Join(", ", request.Requirements),
            EstimatedRange = formattedRange,
            RetellCallId = request.CallId,
            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Fire-and-forget logging to not block Retell execution latency, but with error catch
        _ = Task.Run(async () =>
        {
            try
            {
                await _sheetsService.AppendLeadAsync(leadRow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background thread failed to write Lead '{Id}' to Google Sheets.", leadId);
            }
        });

        return new QuoteResponseModel
        {
            Status = "success",
            LeadId = leadId,
            EstimatedRange = formattedRange
        };
    }

    /// <summary>
    /// Calculates the base pricing matrix estimate by checking unit costs and additional charges.
    /// </summary>
    private decimal CalculateBaseMatrixCost(QuoteRequestModel request, List<PricingMatrixRow> matrix)
    {
        if (matrix == null || matrix.Count == 0)
        {
            // Default pricing rule if pricing matrix is empty
            return (request.GuestCount * 1200m) + 15000m;
        }

        decimal total = 0;
        var matchedAny = false;

        foreach (var req in request.Requirements)
        {
            var match = matrix.FirstOrDefault(m => m.ServiceName.Equals(req, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                matchedAny = true;
                if (match.PricingRules.Equals("PerGuest", StringComparison.OrdinalIgnoreCase))
                {
                    total += (match.UnitCost * request.GuestCount) + match.AdditionalCharges;
                }
                else
                {
                    total += match.UnitCost + match.AdditionalCharges;
                }
            }
        }

        // If no explicit services matched, apply a default general package
        if (!matchedAny)
        {
            total = (request.GuestCount * 1500m) + 30000m;
        }

        return total;
    }

    /// <summary>
    /// Locates comparable past quotation records based on Event Type, Guest Count range (+/- 20%), and Location.
    /// </summary>
    private List<PastQuotationRow> LocateSimilarQuotations(QuoteRequestModel request, List<PastQuotationRow> history)
    {
        if (history == null || history.Count == 0)
        {
            return new List<PastQuotationRow>();
        }

        // 1. Initial filter: same Event Type and Guest Count within +/- 20%
        var candidates = history.Where(q => 
            q.EventType.Equals(request.EventType, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(q.GuestCount - request.GuestCount) <= 0.20 * request.GuestCount
        ).ToList();

        if (candidates.Count == 0)
        {
            return candidates;
        }

        // 2. Specific Location filter (if available and matching records exist)
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var locationMatches = candidates.Where(q => 
                q.Location.Equals(request.Location, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (locationMatches.Count > 0)
            {
                return locationMatches; // Filtered to same location
            }
        }

        return candidates; // Fallback to event type + guest count matches only
    }

    /// <summary>
    /// Formats monetary decimals to Indian currency styling (e.g. ₹4,75,000).
    /// </summary>
    private string FormatCurrency(decimal amount)
    {
        return string.Format(IndianCulture, "₹{0:N0}", amount);
    }
}
