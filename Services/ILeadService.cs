using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service interface for lead workflows.
/// Coordinates lead creation and status/estimate updates.
/// </summary>
public interface ILeadService
{
    /// <summary>
    /// Creates a new lead inside the CRM sheets and generates the next sequential Lead ID.
    /// </summary>
    /// <param name="request">Creation parameters.</param>
    /// <returns>A response containing the generated Lead ID.</returns>
    Task<CreateLeadResponse> CreateLeadAsync(CreateLeadRequest request);

    /// <summary>
    /// Updates the quote range and sets status to "Quoted" for a lead.
    /// </summary>
    /// <param name="request">Update parameters including Lead ID.</param>
    /// <returns>True if successfully updated, false otherwise.</returns>
    Task<bool> UpdateLeadEstimateAsync(UpdateLeadEstimateRequest request);
}
