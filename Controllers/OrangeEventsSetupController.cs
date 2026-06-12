using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RetellIntegrationApi.Models;
using RetellIntegrationApi.Services;

namespace RetellIntegrationApi.Controllers;

/// <summary>
/// Controller for initializing and configuring Google Sheets integration.
/// </summary>
[ApiController]
[Route("api/setup")]
public sealed class OrangeEventsSetupController : ControllerBase
{
    private readonly IOrangeEventsSheetSetupService _setupService;
    private readonly ILogger<OrangeEventsSetupController> _logger;

    public OrangeEventsSetupController(
        IOrangeEventsSheetSetupService setupService,
        ILogger<OrangeEventsSetupController> logger)
    {
        _setupService = setupService;
        _logger = logger;
    }

    /// <summary>
    /// Dynamic, idempotent initialization of the Google Sheets layout for the Orange Events AI system.
    /// Safely creates the spreadsheet and required sheets, inserts headers, and registers sample rows.
    /// </summary>
    /// <response code="200">Google Spreadsheet was validated or newly created successfully.</response>
    /// <response code="500">Internal server error if a critical failure occurred.</response>
    [HttpPost("orange-events")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SetupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetupOrangeEventsSheet()
    {
        try
        {
            _logger.LogInformation("HTTP POST request received at /api/setup/orange-events");
            var result = await _setupService.SetupSpreadsheetAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup failure occurred at /api/setup/orange-events");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }
}
