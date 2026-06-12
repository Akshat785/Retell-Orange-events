using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Service interface for logging registered customer complaints directly into Google Sheets.
/// </summary>
public interface IComplaintGoogleSheetsService
{
    /// <summary>
    /// Appends a new complaint row into the designated Google Sheets complaints sheet.
    /// </summary>
    /// <param name="row">The detailed complaint row.</param>
    Task AppendComplaintRowAsync(ComplaintSheetRow row);
}
