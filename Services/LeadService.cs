using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service implementation coordinating high-level Lead operations.
/// </summary>
public class LeadService : ILeadService
{
    private readonly IGoogleSheetsService _sheetsService;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        IGoogleSheetsService sheetsService,
        ILogger<LeadService> logger)
    {
        _sheetsService = sheetsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateLeadResponse> CreateLeadAsync(CreateLeadRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogInformation("Coordinating creation of Lead for customer '{Name}'...", request.CustomerName);
            var leadId = await _sheetsService.CreateLeadAsync(request);

            return new CreateLeadResponse
            {
                LeadId = leadId,
                Message = "Lead created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordination failure: Lead creation failed for customer '{Name}'.", request.CustomerName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateLeadEstimateAsync(UpdateLeadEstimateRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            _logger.LogInformation("Coordinating estimate update for Lead '{LeadId}'...", request.LeadId);
            await _sheetsService.UpdateLeadEstimateAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordination failure: Estimate update failed for Lead '{LeadId}'.", request.LeadId);
            throw;
        }
    }
}
