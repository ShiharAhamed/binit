using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Join entity defining accepted waste categories for a roadside bin.
/// Enables multi-stream waste disposal at a single bin station.
/// </summary>
public class WasteBinAcceptedWasteType
{
    public Guid WasteBinId { get; set; }
    public WasteBin? WasteBin { get; set; }

    public WasteType WasteType { get; set; }
}
