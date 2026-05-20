using System;
using System.Collections.Generic;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.Enums;

namespace Mets.Replenishment.Infrastructure.Data;

public static class DbSeeder
{
    public static void Seed(ReplenishmentDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Requests.Any()) return;

        var req1Id = Guid.NewGuid();
        var req2Id = Guid.NewGuid();
        var req3Id = Guid.NewGuid();

        var requests = new List<ReplenishmentRequest>
        {
            new ReplenishmentRequest
            {
                Id = req1Id,
                Location = "Station A1",
                Priority = RequestPriority.Urgent,
                Status = RequestStatus.Draft,
                CreatedBy = "Worker John",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-1),
                Items = new List<ReplenishmentRequestItem>
                {
                    new ReplenishmentRequestItem { RequestId = req1Id, ArticleNumber = "ART-100", Description = "Screws 5mm", RequestedQuantity = 500 },
                    new ReplenishmentRequestItem { RequestId = req1Id, ArticleNumber = "ART-101", Description = "Metal Plates", RequestedQuantity = 100 }
                }
            },
            new ReplenishmentRequest
            {
                Id = req2Id,
                Location = "Station B2",
                Priority = RequestPriority.Normal,
                Status = RequestStatus.Submitted,
                ValidationStatus = ValidationStatus.Completed,
                CreatedBy = "Worker Bob",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                SubmittedAt = DateTime.UtcNow.AddHours(-20),
                UpdatedAt = DateTime.UtcNow.AddHours(-20),
                Items = new List<ReplenishmentRequestItem>
                {
                    new ReplenishmentRequestItem { RequestId = req2Id, ArticleNumber = "ART-205", Description = "Wiring Harness", RequestedQuantity = 50, StockAvailable = 150 }
                }
            },
            new ReplenishmentRequest
            {
                Id = req3Id,
                Location = "Station C3",
                Priority = RequestPriority.Low,
                Status = RequestStatus.Approved,
                ValidationStatus = ValidationStatus.Completed,
                CreatedBy = "Worker Alice",
                ReviewedBy = "Reviewer Sarah",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                SubmittedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                Items = new List<ReplenishmentRequestItem>
                {
                    new ReplenishmentRequestItem { RequestId = req3Id, ArticleNumber = "ART-300", Description = "Control Boards", RequestedQuantity = 20, StockAvailable = 25 }
                }
            }
        };

        context.Requests.AddRange(requests);
        context.SaveChanges();
    }
}
