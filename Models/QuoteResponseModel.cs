using System.Text.Json.Serialization;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing the response returned to Retell AI containing the estimated range.
/// </summary>
public sealed class QuoteResponseModel
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    [JsonPropertyName("lead_id")]
    public string LeadId { get; set; } = string.Empty;

    [JsonPropertyName("estimated_range")]
    public string EstimatedRange { get; set; } = string.Empty;

    [JsonPropertyName("caveat_message")]
    public string CaveatMessage { get; set; } = "Please note this is only an estimate based on our pricing matrix and past events. Our sales team will follow up separately with a finalized quotation.";
}
