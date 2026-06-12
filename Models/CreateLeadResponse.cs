namespace RetellIntegrationApi.Models;

/// <summary>
/// Response model returned after successful lead creation.
/// </summary>
public class CreateLeadResponse
{
    public string LeadId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
