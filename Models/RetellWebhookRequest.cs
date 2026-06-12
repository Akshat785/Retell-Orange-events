using System.Text.Json.Serialization;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Root model for the incoming Retell AI webhook request payload.
/// </summary>
public sealed class RetellWebhookRequest
{
    /// <summary>
    /// Type of webhook event (e.g., call_started, call_ended, transcript_updated, call_analyzed).
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// Detailed call information payload.
    /// </summary>
    [JsonPropertyName("call")]
    public RetellCall? Call { get; set; }
}

/// <summary>
/// Detailed call parameters provided by Retell AI.
/// </summary>
public sealed class RetellCall
{
    /// <summary>
    /// Unique identifier of the call.
    /// </summary>
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = string.Empty;

    /// <summary>
    /// The agent identifier handling the call.
    /// </summary>
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the call (e.g., registered, ongoing, ended).
    /// </summary>
    [JsonPropertyName("call_status")]
    public string CallStatus { get; set; } = string.Empty;

    /// <summary>
    /// Epoch millisecond start time.
    /// </summary>
    [JsonPropertyName("start_timestamp")]
    public long StartTimestamp { get; set; }

    /// <summary>
    /// Epoch millisecond end time.
    /// </summary>
    [JsonPropertyName("end_timestamp")]
    public long EndTimestamp { get; set; }

    /// <summary>
    /// Duration of the call in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    /// <summary>
    /// Full plain-text or structured transcript string.
    /// </summary>
    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    /// <summary>
    /// Standard Retell user_number parameter.
    /// </summary>
    [JsonPropertyName("user_number")]
    public string UserNumber { get; set; } = string.Empty;

    /// <summary>
    /// Call origin phone number (fallback).
    /// </summary>
    [JsonPropertyName("from_number")]
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// Call destination phone number (fallback).
    /// </summary>
    [JsonPropertyName("to_number")]
    public string ToNumber { get; set; } = string.Empty;

    // --- Dynamic AI Extraction Fields (Complaints) ---

    /// <summary>
    /// Optional post-call analysis custom data block containing variables extracted by AI.
    /// </summary>
    [JsonPropertyName("post_call_analysis")]
    public RetellPostCallAnalysis? PostCallAnalysis { get; set; }

    /// <summary>
    /// Alternative post-call analysis property key sometimes used by Retell.
    /// </summary>
    [JsonPropertyName("call_analysis")]
    public RetellCallAnalysis? CallAnalysis { get; set; }

    /// <summary>
    /// Direct variable fallback - Customer name.
    /// </summary>
    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Direct variable fallback - Mobile number.
    /// </summary>
    [JsonPropertyName("mobile_number")]
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Direct variable fallback - Complaint type.
    /// </summary>
    [JsonPropertyName("complaint_type")]
    public string ComplaintType { get; set; } = string.Empty;

    /// <summary>
    /// Direct variable fallback - Complaint description.
    /// </summary>
    [JsonPropertyName("complaint_description")]
    public string ComplaintDescription { get; set; } = string.Empty;

    /// <summary>
    /// Direct variable fallback - Address.
    /// </summary>
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating if a complaint was registered. Supports both boolean and string representations.
    /// </summary>
    [JsonPropertyName("complaint_registered")]
    public object? ComplaintRegistered { get; set; }
}

/// <summary>
/// Retell Post-Call Analysis block.
/// </summary>
public sealed class RetellPostCallAnalysis
{
    [JsonPropertyName("custom_analysis_data")]
    public ComplaintExtractionData? CustomAnalysisData { get; set; }
}

/// <summary>
/// Retell Call Analysis block.
/// </summary>
public sealed class RetellCallAnalysis
{
    [JsonPropertyName("custom_analysis_data")]
    public ComplaintExtractionData? CustomAnalysisData { get; set; }
}

/// <summary>
/// Custom AI Extracted variables for complaint registration.
/// </summary>
public sealed class ComplaintExtractionData
{
    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("mobile_number")]
    public string MobileNumber { get; set; } = string.Empty;

    [JsonPropertyName("complaint_type")]
    public string ComplaintType { get; set; } = string.Empty;

    [JsonPropertyName("complaint_description")]
    public string ComplaintDescription { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating if a complaint was registered. Supports both boolean and string representations.
    /// </summary>
    [JsonPropertyName("complaint_registered")]
    public object? ComplaintRegistered { get; set; }

    // --- Orange Events Variables ---

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("event_date")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("guest_count")]
    public object? GuestCount { get; set; }

    [JsonPropertyName("requirements")]
    public object? Requirements { get; set; }
}
