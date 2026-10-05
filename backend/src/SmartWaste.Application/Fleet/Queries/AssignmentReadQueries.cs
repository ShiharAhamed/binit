using SmartWaste.Domain.Collection.Enums;
namespace SmartWaste.Application.Collection.Queries;
public sealed class AssignmentListQuery { public CollectionAssignmentStatus? Status { get; set; } public int Page { get; set; } = 1; public int PageSize { get; set; } = 20; }
public sealed class AvailableAssignmentTaskQuery { public int Page { get; set; } = 1; public int PageSize { get; set; } = 20; }
