using System;
using System.Collections.Generic;
using Mets.Replenishment.Core.Enums;

namespace Mets.Replenishment.Core.Entities;

public class ReplenishmentRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Location { get; set; } = string.Empty;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.NotStarted;
    
    public string? RejectionReason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<ReplenishmentRequestItem> Items { get; set; } = new();
}
