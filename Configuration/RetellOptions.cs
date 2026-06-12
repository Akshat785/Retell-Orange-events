namespace RetellIntegrationApi.Configuration;

/// <summary>
/// Configuration options for Retell AI integration.
/// </summary>
public sealed class RetellOptions
{
    public const string Retell = "Retell";

    public string ApiKey { get; set; } = string.Empty;
    public string WebhookApiKey { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
}
