using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.DTOs;

namespace Mets.Replenishment.Core.Interfaces;

public interface IReplenishmentService
{
    Task<PagedResult<ReplenishmentRequest>> GetRequestsAsync(string? status, string? priority, string? location, int pageNumber = 1, int pageSize = 20);
    Task<ReplenishmentRequest?> GetRequestByIdAsync(Guid id);
    Task<ReplenishmentRequest> CreateDraftAsync(ReplenishmentRequest request);
    Task SubmitRequestAsync(Guid id);
    Task<ReplenishmentRequest> ApproveRequestAsync(Guid id, string? reviewerName);
    Task<ReplenishmentRequest> RejectRequestAsync(Guid id, string? reviewerName, string reason);
    Task<ReplenishmentRequest> FulfillRequestAsync(Guid id, Dictionary<Guid, int> fulfilledQuantities);
}
