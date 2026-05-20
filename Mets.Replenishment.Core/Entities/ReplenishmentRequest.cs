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
    
    public string CreatedBy { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<ReplenishmentRequestItem> Items { get; set; } = new();

    public void Submit()
    {
        if (Status != RequestStatus.Draft)
            throw new InvalidOperationException("Only draft requests can be submitted.");

        Status = RequestStatus.Submitted;
        ValidationStatus = ValidationStatus.Pending;
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(string? reviewerName)
    {
        if (Status != RequestStatus.Submitted || ValidationStatus != ValidationStatus.Completed)
            throw new InvalidOperationException("Request must be submitted and validated before approval.");

        Status = RequestStatus.Approved;
        ReviewedBy = reviewerName ?? "System Reviewer";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string? reviewerName, string reason)
    {
        if (Status != RequestStatus.Submitted)
            throw new InvalidOperationException("Request must be submitted before rejection.");

        Status = RequestStatus.Rejected;
        RejectionReason = reason;
        ReviewedBy = reviewerName ?? "System Reviewer";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fulfill(Dictionary<Guid, int> fulfilledQuantities)
    {
        if (Status != RequestStatus.Approved)
            throw new InvalidOperationException("Only approved requests can be fulfilled.");

        foreach (var item in Items)
        {
            if (fulfilledQuantities.TryGetValue(item.Id, out var qty))
            {
                item.FulfilledQuantity = qty;
            }
        }

        Status = RequestStatus.Fulfilled;
        UpdatedAt = DateTime.UtcNow;
    }
}
