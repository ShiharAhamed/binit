namespace SmartWaste.Domain.Collection.Enums;

/// <summary>
/// Operational reason justifying the creation of a collection task.
/// </summary>
public enum CollectionReason
{
    VerifiedReport,
    FullOrBlockedBin,
    RoutineCollection,
    OfficerDiscretion
}
