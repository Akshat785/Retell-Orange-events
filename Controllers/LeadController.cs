using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RetellIntegrationApi.Models;
using RetellIntegrationApi.Services;

namespace RetellIntegrationApi.Controllers;

/// <summary>
/// Controller handling CRM Lead operations for the sales lifecycle.
/// </summary>
[ApiController]
[Route("api/retell/leads")]
public class LeadController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ILogger<LeadController> _logger;

    public LeadController(
        ILeadService leadService,
        ILogger<LeadController> logger)
    {
        _leadService = leadService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new customer inquiry inside the Leads CRM sheets.
    /// Generates a sequential LEAD-XXXXXX ID by locating the highest existing LeadId.
    /// </summary>
    /// <response code="200">Lead created successfully and sequential ID generated.</response>
    /// <response code="400">Bad request if body is missing.</response>
    /// <response code="500">Internal server error if sheet appending fails.</response>
    [HttpPost("create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CreateLeadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request payload cannot be null." });
        }

        try
        {
            _logger.LogInformation("HTTP POST: Creating Lead for customer '{Name}'", request.CustomerName);
            var response = await _leadService.CreateLeadAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Failed to create Lead for customer '{Name}'", request.CustomerName);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates a lead's record with a calculated quotation estimate range and changes status to "Quoted".
    /// </summary>
    /// <response code="200">Estimate registered and Lead Status updated successfully.</response>
    /// <response code="400">Bad request if payload or LeadId is missing.</response>
    /// <response code="404">Not Found if LeadId does not match any row in the spreadsheet.</response>
    /// <response code="500">Internal server error if write operations fail.</response>
    [HttpPost("update-estimate")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEstimate([FromBody] UpdateLeadEstimateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.LeadId))
        {
            return BadRequest(new { error = "Request body and LeadId are required parameters." });
        }

        try
        {
            _logger.LogInformation("HTTP POST: Registering estimate update for Lead '{LeadId}'", request.LeadId);
            await _leadService.UpdateLeadEstimateAsync(request);
            return Ok(new { success = true });
        }
        catch (System.Collections.Generic.KeyNotFoundException kex)
        {
            _logger.LogWarning(kex, "API Warning: Lead ID '{LeadId}' not found.", request.LeadId);
            return NotFound(new { error = kex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Error: Failed to update estimate for Lead '{LeadId}'", request.LeadId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }
}
