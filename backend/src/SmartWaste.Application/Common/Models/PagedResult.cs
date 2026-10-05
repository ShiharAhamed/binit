namespace SmartWaste.Application.Common.Models;

/// <summary>
/// Standard pagination response envelope conforming to api-contract.md.
/// </summary>
/// <typeparam name="T">Payload item type</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
