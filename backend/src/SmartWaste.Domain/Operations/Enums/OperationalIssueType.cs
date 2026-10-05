namespace SmartWaste.Domain.Operations.Enums;

/// <summary>
/// Type classification of operational issues reported by drivers in the field.
/// </summary>
public enum OperationalIssueType
{
    VehicleProblem = 0,
    RoadOrAccessIssue = 1,
    EquipmentProblem = 2,
    SafetyConcern = 3,
    OperationalDelay = 4,
    Other = 5
}
