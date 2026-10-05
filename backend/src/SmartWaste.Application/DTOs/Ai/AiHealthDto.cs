namespace SmartWaste.Application.DTOs.Ai;

/// <summary>
/// Strongly-typed model representing the health status of the internal FastAPI AI service.
/// </summary>
public class AiHealthDto
{
    public string Status { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
}
