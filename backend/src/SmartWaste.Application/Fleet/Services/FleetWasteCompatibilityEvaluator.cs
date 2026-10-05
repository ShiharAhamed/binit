using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Domain.Collection.Entities;

namespace SmartWaste.Application.Fleet.Services;

/// <summary>
/// Authoritative domain evaluator for collection task waste handling requirements
/// vs vehicle supported waste types.
/// Reused across assignment creation and internal AI compatibility checks.
/// </summary>
public static class FleetWasteCompatibilityEvaluator
{
    public static FleetCompatibilityResultDto Evaluate(IEnumerable<CollectionTask> tasks, Vehicle vehicle)
    {
        var supported = vehicle.SupportedWasteTypes.Select(x => x.WasteType).ToHashSet();
        var issues = new List<string>();
        bool hasIncompatible = false;
        bool hasUnknown = false;

        foreach (var t in tasks)
        {
            if (t.WasteReport is not null)
            {
                if (!supported.Contains(t.WasteReport.WasteType))
                {
                    hasIncompatible = true;
                    issues.Add($"Task '{t.TaskCode}' requires {t.WasteReport.WasteType} waste collection, which is not supported by vehicle '{vehicle.RegistrationNumber}'.");
                }
            }

            if (t.WasteBin is not null)
            {
                if (!t.WasteBin.AcceptedWasteTypes.Any())
                {
                    hasUnknown = true;
                    issues.Add($"Task '{t.TaskCode}' target bin '{t.WasteBin.BinCode}' has no registered accepted waste types; waste-handling uncertainty requires an officer acknowledgement.");
                }
                else if (!t.WasteBin.AcceptedWasteTypes.Any(x => supported.Contains(x.WasteType)))
                {
                    hasIncompatible = true;
                    var binTypes = string.Join(", ", t.WasteBin.AcceptedWasteTypes.Select(x => x.WasteType.ToString()));
                    issues.Add($"Task '{t.TaskCode}' target bin '{t.WasteBin.BinCode}' accepts ({binTypes}), none of which are supported by vehicle '{vehicle.RegistrationNumber}'.");
                }
                else if (t.WasteBin.AcceptedWasteTypes.Any(x => !supported.Contains(x.WasteType)))
                {
                    hasUnknown = true;
                    var supportedBinTypes = string.Join(", ", t.WasteBin.AcceptedWasteTypes.Where(x => supported.Contains(x.WasteType)).Select(x => x.WasteType.ToString()));
                    var unsupportedBinTypes = string.Join(", ", t.WasteBin.AcceptedWasteTypes.Where(x => !supported.Contains(x.WasteType)).Select(x => x.WasteType.ToString()));
                    issues.Add($"Task '{t.TaskCode}' target bin '{t.WasteBin.BinCode}' accepts mixed waste types with partial vehicle compatibility (supported: {supportedBinTypes}; unsupported: {unsupportedBinTypes}); waste-handling uncertainty requires an officer acknowledgement.");
                }
            }

            if (t.WasteReport is null && t.WasteBin is null && !t.WasteReportId.HasValue && !t.WasteBinId.HasValue)
            {
                hasUnknown = true;
                issues.Add($"Task '{t.TaskCode}' has no registered waste report or bin target; waste-handling uncertainty requires an officer acknowledgement.");
            }
        }

        if (hasIncompatible)
        {
            return new FleetCompatibilityResultDto
            {
                Status = FleetCompatibilityStatus.Incompatible,
                RequiresAcknowledgement = false,
                Issues = issues
            };
        }

        if (hasUnknown)
        {
            return new FleetCompatibilityResultDto
            {
                Status = FleetCompatibilityStatus.Unknown,
                RequiresAcknowledgement = true,
                Issues = issues
            };
        }

        return new FleetCompatibilityResultDto
        {
            Status = FleetCompatibilityStatus.Compatible,
            RequiresAcknowledgement = false,
            Issues = Array.Empty<string>()
        };
    }
}
