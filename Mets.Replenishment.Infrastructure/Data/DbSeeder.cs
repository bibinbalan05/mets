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

        var requests = new List<ReplenishmentRequest>();
        var random = new Random(42);
        
        string[] locations = { "Station A1", "Station B2", "Station C3", "Station D4", "Warehouse West", "Storage Area 5" };
        RequestPriority[] priorities = { RequestPriority.Low, RequestPriority.Normal, RequestPriority.Urgent };
        RequestStatus[] statuses = { RequestStatus.Draft, RequestStatus.Submitted, RequestStatus.Approved, RequestStatus.Rejected, RequestStatus.Fulfilled };
        string[] workers = { "Worker John", "Worker Bob", "Worker Alice", "Worker Charlie", "Worker Diana" };
        string[] reviewers = { "Reviewer Sarah", "Reviewer Dave", "Reviewer James" };

        for (int i = 1; i <= 25; i++)
        {
            var id = Guid.NewGuid();
            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];
            var location = locations[random.Next(locations.Length)] + $" (Row {i})";
            var worker = workers[random.Next(workers.Length)];
            var reviewer = (status != RequestStatus.Draft && status != RequestStatus.Submitted) ? reviewers[random.Next(reviewers.Length)] : null;
            
            var req = new ReplenishmentRequest
            {
                Id = id,
                Location = location,
                Priority = priority,
                Status = status,
                CreatedBy = worker,
                ReviewedBy = reviewer,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 45),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i * 45),
                ValidationStatus = status == RequestStatus.Draft ? ValidationStatus.NotStarted : ValidationStatus.Completed,
                Items = new List<ReplenishmentRequestItem>
                {
                    new ReplenishmentRequestItem 
                    { 
                        Id = Guid.NewGuid(),
                        RequestId = id, 
                        ArticleNumber = $"ART-{(100 + i)}", 
                        Description = $"Seeded Item {i}", 
                        RequestedQuantity = random.Next(10, 100), 
                        StockAvailable = random.Next(10, 200) 
                    }
                }
            };
            
            if (status != RequestStatus.Draft)
            {
                req.SubmittedAt = req.CreatedAt.AddMinutes(10);
            }
            
            requests.Add(req);
        }

        context.Requests.AddRange(requests);
        context.SaveChanges();
    }
}
