namespace Mets.Replenishment.Core.DTOs;

public class RejectRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? ReviewerName { get; set; }
}
