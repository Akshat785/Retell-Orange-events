using System.Threading.Tasks;
using RetellIntegrationApi.Models;

namespace RetellIntegrationApi.Services;

/// <summary>
/// Interface for the service that automates Google Sheets setup for Orange Events data.
/// Handles dynamic spreadsheet and sheet/tab creation, header population, sample data insertion,
/// and dynamic configuration writes.
/// </summary>
public interface IOrangeEventsSheetSetupService
{
    /// <summary>
    /// Checks, creates, and configures the Orange Events Google Spreadsheet and its required tabs.
    /// Saves the new Spreadsheet ID dynamically to appsettings if created.
    /// </summary>
    /// <returns>A SetupResult detailing whether the spreadsheet is new, existing, or configured.</returns>
    Task<SetupResult> SetupSpreadsheetAsync();
}
