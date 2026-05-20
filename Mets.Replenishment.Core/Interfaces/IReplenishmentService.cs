using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mets.Replenishment.Core.Entities;

namespace Mets.Replenishment.Core.Interfaces;

public interface IReplenishmentService
{
    Task<IEnumerable<ReplenishmentRequest>> GetRequestsAsync(string? status, string? priority, string? location);
    Task<ReplenishmentRequest?> GetRequestByIdAsync(Guid id);
    Task<ReplenishmentRequest> CreateDraftAsync(ReplenishmentRequest request);
    Task SubmitRequestAsync(Guid id);
    Task<ReplenishmentRequest> ApproveRequestAsync(Guid id, string? reviewerName);
    Task<ReplenishmentRequest> RejectRequestAsync(Guid id, string? reviewerName, string reason);
    Task<ReplenishmentRequest> FulfillRequestAsync(Guid id, Dictionary<Guid, int> fulfilledQuantities);
}
