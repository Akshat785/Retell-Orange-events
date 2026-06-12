using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetellIntegrationApi.Configuration;
using RetellIntegrationApi.Models;
using RetellIntegrationApi.Services;

namespace RetellIntegrationApi.Controllers;

/// <summary>
/// API Controller that handles incoming Webhook data sent by Retell AI voice agents.
/// Includes support for general call logging and AI-based complaint registration workflows.
/// </summary>
[ApiController]
[Route("api/retell")]
public sealed class RetellWebhookController : ControllerBase
{
    private readonly IGoogleSheetsService _sheetsService;
    private readonly IComplaintGoogleSheetsService _complaintSheetsService;
    private readonly IEventSheetsService _eventSheetsService;
    private readonly RetellOptions _retellOptions;
    private readonly ILogger<RetellWebhookController> _logger;

    public RetellWebhookController(
        IGoogleSheetsService sheetsService,
        IComplaintGoogleSheetsService complaintSheetsService,
        IEventSheetsService eventSheetsService,
        IOptions<RetellOptions> retellOptions,
        ILogger<RetellWebhookController> logger)
    {
        _sheetsService = sheetsService;
        _complaintSheetsService = complaintSheetsService;
        _eventSheetsService = eventSheetsService;
        _retellOptions = retellOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint to receive webhook payloads from Retell AI.
    /// Extracts call metadata (ID, number, duration, transcript) and either logs a complaint or logs call metrics into Google Sheets.
    /// </summary>
    /// <param name="payload">The webhook payload containing call metrics and AI-extracted variables.</param>
    /// <response code="200">Webhook processed successfully.</response>
    /// <response code="400">Bad request if the payload or call body is invalid.</response>
    /// <response code="401">Unauthorized if the API key check fails.</response>
    [HttpPost("webhook")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HandleWebhook([FromBody] RetellWebhookRequest payload)
    {
        if (payload == null || payload.Call == null)
        {
            _logger.LogWarning("Received an empty or incomplete webhook payload.");
            return BadRequest(new { error = "Invalid webhook payload structure. The 'call' object is missing." });
        }

        // 1. Log the full request payload to the console for inspection/debugging
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var rawPayloadString = JsonSerializer.Serialize(payload, jsonOptions);
        _logger.LogInformation("Received Retell Webhook Event '{Event}'. Full Payload:\n{Payload}", 
            payload.Event, rawPayloadString);

        var call = payload.Call;
        var callId = call.CallId;
        var rawTranscript = call.Transcript;

        // 2. Extract Caller Number (fallback across user_number, from_number, to_number)
        var callerNumber = !string.IsNullOrWhiteSpace(call.UserNumber) ? call.UserNumber
                         : !string.IsNullOrWhiteSpace(call.FromNumber) ? call.FromNumber
                         : !string.IsNullOrWhiteSpace(call.ToNumber) ? call.ToNumber
                         : "Unknown Caller";

        // 3. Extract and Format Duration from raw milliseconds (e.g. 135000ms -> "2m 15s")
        var durationSpan = TimeSpan.FromMilliseconds(call.DurationMs);
        var durationFormatted = durationSpan.TotalSeconds >= 60
            ? $"{(int)durationSpan.TotalMinutes}m {durationSpan.Seconds}s"
            : $"{durationSpan.Seconds}s";

        _logger.LogInformation("Extracted Webhook Metadata:\n" +
                            "- Call ID: {CallId}\n" +
                            "- Caller Number: {CallerNumber}\n" +
                            "- Duration: {Duration} (Raw: {RawDurationMs} ms)\n" +
                            "- Transcript snippet: {TranscriptLength} characters",
            callId, callerNumber, durationFormatted, call.DurationMs, rawTranscript.Length);

        // 4. Extract Structured Complaint Fields from direct fields or post-call analysis
        var customerName = !string.IsNullOrWhiteSpace(call.CustomerName) ? call.CustomerName
                         : !string.IsNullOrWhiteSpace(call.PostCallAnalysis?.CustomAnalysisData?.CustomerName) ? call.PostCallAnalysis.CustomAnalysisData.CustomerName
                         : call.CallAnalysis?.CustomAnalysisData?.CustomerName ?? string.Empty;

        var mobileNumber = !string.IsNullOrWhiteSpace(call.MobileNumber) ? call.MobileNumber
                         : !string.IsNullOrWhiteSpace(call.PostCallAnalysis?.CustomAnalysisData?.MobileNumber) ? call.PostCallAnalysis.CustomAnalysisData.MobileNumber
                         : call.CallAnalysis?.CustomAnalysisData?.MobileNumber ?? string.Empty;

        var complaintType = !string.IsNullOrWhiteSpace(call.ComplaintType) ? call.ComplaintType
                          : !string.IsNullOrWhiteSpace(call.PostCallAnalysis?.CustomAnalysisData?.ComplaintType) ? call.PostCallAnalysis.CustomAnalysisData.ComplaintType
                          : call.CallAnalysis?.CustomAnalysisData?.ComplaintType ?? string.Empty;

        var complaintDescription = !string.IsNullOrWhiteSpace(call.ComplaintDescription) ? call.ComplaintDescription
                                 : !string.IsNullOrWhiteSpace(call.PostCallAnalysis?.CustomAnalysisData?.ComplaintDescription) ? call.PostCallAnalysis.CustomAnalysisData.ComplaintDescription
                                 : call.CallAnalysis?.CustomAnalysisData?.ComplaintDescription ?? string.Empty;

        var address = !string.IsNullOrWhiteSpace(call.Address) ? call.Address
                    : !string.IsNullOrWhiteSpace(call.PostCallAnalysis?.CustomAnalysisData?.Address) ? call.PostCallAnalysis.CustomAnalysisData.Address
                    : call.CallAnalysis?.CustomAnalysisData?.Address ?? string.Empty;

        var complaintRegistered = ParseComplaintRegistered(call.ComplaintRegistered)
                                || ParseComplaintRegistered(call.PostCallAnalysis?.CustomAnalysisData?.ComplaintRegistered)
                                || ParseComplaintRegistered(call.CallAnalysis?.CustomAnalysisData?.ComplaintRegistered);

        // 5. Route request based on Complaint Registration status
        if (complaintRegistered)
        {
            _logger.LogInformation("Complaint registration detected for Customer '{CustomerName}'. Validating mobile number...", customerName);

            if (!IsValidIndianMobile(mobileNumber))
            {
                _logger.LogWarning("Complaint registration failed. Invalid Indian mobile number: '{MobileNumber}' for customer '{CustomerName}'.", mobileNumber, customerName);
                return Ok(new
                {
                    status = "failed",
                    message = "Invalid mobile number"
                });
            }

            var complaintRow = new ComplaintSheetRow
            {
                CustomerName = customerName,
                MobileNumber = mobileNumber,
                ComplaintType = complaintType,
                ComplaintDescription = complaintDescription,
                Address = address,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            try
            {
                await _complaintSheetsService.AppendComplaintRowAsync(complaintRow);
                _logger.LogInformation("Complaint for customer '{CustomerName}' successfully written to Google Sheets.", customerName);

                return Ok(new
                {
                    status = "success",
                    message = "Complaint registered successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log complaint for customer '{CustomerName}' to Google Sheets.", customerName);
                // Return failure status but keep HTTP 200 to prevent endless webhook retries
                return Ok(new
                {
                    status = "failed",
                    message = "Complaint registration failed to write to log sheet."
                });
            }
        }

        // 5.5 Event Enquiry Handling: If call is from the Event Agent, capture and write structured Lead info
        if (call.AgentId.Equals(_retellOptions.AgentId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Event enquiry call detected from AgentId '{AgentId}'. Logging Lead info...", call.AgentId);

            var email = call.PostCallAnalysis?.CustomAnalysisData?.Email
                     ?? call.CallAnalysis?.CustomAnalysisData?.Email ?? string.Empty;

            var eventType = call.PostCallAnalysis?.CustomAnalysisData?.EventType
                         ?? call.CallAnalysis?.CustomAnalysisData?.EventType ?? string.Empty;

            var eventDate = call.PostCallAnalysis?.CustomAnalysisData?.EventDate
                         ?? call.CallAnalysis?.CustomAnalysisData?.EventDate ?? string.Empty;

            var location = call.PostCallAnalysis?.CustomAnalysisData?.Location
                        ?? call.CallAnalysis?.CustomAnalysisData?.Location ?? string.Empty;

            var guestCountObj = call.PostCallAnalysis?.CustomAnalysisData?.GuestCount
                             ?? call.CallAnalysis?.CustomAnalysisData?.GuestCount;
            
            var guestCount = 100;
            if (guestCountObj != null)
            {
                if (guestCountObj is int gInt)
                {
                    guestCount = gInt;
                }
                else if (guestCountObj is JsonElement je && je.ValueKind == JsonValueKind.Number)
                {
                    if (je.TryGetInt32(out var parsedInt)) guestCount = parsedInt;
                }
                else if (int.TryParse(guestCountObj.ToString(), out var parsedInt))
                {
                    guestCount = parsedInt;
                }
            }

            var requirementsObj = call.PostCallAnalysis?.CustomAnalysisData?.Requirements
                               ?? call.CallAnalysis?.CustomAnalysisData?.Requirements;
            
            var requirements = string.Empty;
            if (requirementsObj != null)
            {
                if (requirementsObj is string sReq)
                {
                    requirements = sReq;
                }
                else if (requirementsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
                {
                    var reqList = new List<string>();
                    foreach (var item in je.EnumerateArray())
                    {
                        reqList.Add(item.GetString() ?? string.Empty);
                    }
                    requirements = string.Join(", ", reqList.Where(s => !string.IsNullOrEmpty(s)));
                }
                else
                {
                    requirements = requirementsObj.ToString() ?? string.Empty;
                }
            }

            var leadId = $"lead_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            var leadRow = new LeadSheetRow
            {
                LeadId = leadId,
                Name = string.IsNullOrWhiteSpace(customerName) ? "Valued Enquirer" : customerName,
                Mobile = string.IsNullOrWhiteSpace(mobileNumber) ? callerNumber : mobileNumber,
                Email = email,
                EventType = eventType,
                EventDate = eventDate,
                Location = location,
                GuestCount = guestCount,
                Requirements = requirements,
                EstimatedRange = "Call Ended (Transcript Logged)",
                RetellCallId = callId,
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            try
            {
                await _eventSheetsService.AppendLeadAsync(leadRow);
                _logger.LogInformation("Enquiry lead for customer '{CustomerName}' successfully written to Google Sheets.", customerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log event lead to Google Sheets for customer '{CustomerName}'.", customerName);
            }
        }

        // 6. Normal Workflow: Log call transcript to the primary Sheets tab
        var sheetRow = new GoogleSheetRow
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            CallerNumber = callerNumber,
            Transcript = rawTranscript,
            Duration = durationFormatted
        };

        try
        {
            await _sheetsService.AppendRowAsync(sheetRow);
            _logger.LogInformation("Call '{CallId}' transcript appended to Google Sheets successfully.", callId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write call '{CallId}' transcript to Google Sheets. Webhook acknowledged to avoid retry loop.", callId);
        }

        // Return standard HTTP 200 response to caller
        return Ok(new
        {
            status = "success",
            message = "Webhook payload processed and recorded successfully.",
            call_id = callId
        });
    }

    /// <summary>
    /// Helper method to validate Indian mobile numbers.
    /// Strips any country code prefixes (+91, 91, or leading 0) and ensures exactly 10 digits starting with [6-9].
    /// </summary>
    /// <param name="mobile">The mobile number string to validate.</param>
    /// <returns>True if valid; otherwise false.</returns>
    private bool IsValidIndianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return false;
        }

        // Remove all non-digit characters (spaces, +, -, parentheses)
        var cleaned = System.Text.RegularExpressions.Regex.Replace(mobile, @"[^\d]", "");

        // If it starts with 91 and has 12 digits, strip the 91 prefix
        if (cleaned.Length == 12 && cleaned.StartsWith("91"))
        {
            cleaned = cleaned.Substring(2);
        }
        // If it starts with 0 and has 11 digits, strip the leading 0
        else if (cleaned.Length == 11 && cleaned.StartsWith("0"))
        {
            cleaned = cleaned.Substring(1);
        }

        // Standard Indian mobile numbers are exactly 10 digits and start with 6, 7, 8, or 9
        return System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[6-9]\d{9}$");
    }

    /// <summary>
    /// Safely parses the complaint_registered value from object/string/bool/JsonElement into a boolean.
    /// Handles string representations like "true", "yes", "1" as true.
    /// </summary>
    private bool ParseComplaintRegistered(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.True) return true;
            if (je.ValueKind == JsonValueKind.False) return false;
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                return "true".Equals(str, StringComparison.OrdinalIgnoreCase) || 
                       "yes".Equals(str, StringComparison.OrdinalIgnoreCase) ||
                       "1".Equals(str);
            }
            if (je.ValueKind == JsonValueKind.Number)
            {
                return je.TryGetInt32(out var val) && val == 1;
            }
        }
        var s = value.ToString();
        return "true".Equals(s, StringComparison.OrdinalIgnoreCase) || 
               "yes".Equals(s, StringComparison.OrdinalIgnoreCase) ||
               "1".Equals(s);
    }
}
