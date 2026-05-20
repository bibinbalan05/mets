using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.DTOs;
using Mets.Replenishment.Core.Enums;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Infrastructure.Data;

namespace Mets.Replenishment.Infrastructure.Services;

public class ReplenishmentService : IReplenishmentService
{
    private readonly ReplenishmentDbContext _context;
    private readonly IValidationJobQueue _queue;
    private readonly ILogger<ReplenishmentService> _logger;

    public ReplenishmentService(
        ReplenishmentDbContext context, 
        IValidationJobQueue queue,
        ILogger<ReplenishmentService> logger)
    {
        _context = context;
        _queue = queue;
        _logger = logger;
    }

    public async Task<PagedResult<ReplenishmentRequest>> GetRequestsAsync(string? status, string? priority, string? location, int pageNumber = 1, int pageSize = 20)
    {
        _logger.LogDebug("Retrieving requests. Filters - Status: {Status}, Priority: {Priority}, Location: {Location}", status, priority, location);
        
        var query = _context.Requests.Include(r => r.Items).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<RequestPriority>(priority, true, out var parsedPriority))
            query = query.Where(r => r.Priority == parsedPriority);
            
        if (!string.IsNullOrEmpty(location))
            query = query.Where(r => r.Location.Contains(location));

        var totalCount = await query.CountAsync();

        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 20 : pageSize;

        List<ReplenishmentRequest> items;
        if (totalCount == 0 || (pageNumber - 1) > (totalCount - 1) / pageSize)
        {
            items = new List<ReplenishmentRequest>();
        }
        else
        {
            items = await query.OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        _logger.LogInformation("Retrieved {Count} replenishment requests (page {PageNumber}/{PageSize}) matching search criteria", items.Count, pageNumber, pageSize);
        return new PagedResult<ReplenishmentRequest>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ReplenishmentRequest?> GetRequestByIdAsync(Guid id)
    {
        _logger.LogDebug("Fetching replenishment request with ID {RequestId}", id);
        return await _context.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<ReplenishmentRequest> CreateDraftAsync(ReplenishmentRequest request)
    {
        _logger.LogInformation("Creating draft replenishment request at {Location} by worker {CreatedBy}", request.Location, request.CreatedBy);

        request.Id = Guid.NewGuid();
        request.Status = RequestStatus.Draft;
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            item.Id = Guid.NewGuid();
            item.RequestId = request.Id;
        }

        _context.Requests.Add(request);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Draft replenishment request {RequestId} created successfully with {ItemCount} items", request.Id, request.Items.Count);
            return request;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist draft replenishment request created by {CreatedBy} at {Location}", request.CreatedBy, request.Location);
            throw;
        }
    }

    public async Task SubmitRequestAsync(Guid id)
    {
        _logger.LogInformation("Attempting to submit request {RequestId}", id);

        var request = await _context.Requests.FindAsync(id);
        if (request == null)
        {
            _logger.LogWarning("Submit failed: Replenishment request {RequestId} not found", id);
            throw new KeyNotFoundException($"Replenishment request with ID {id} not found.");
        }

        try
        {
            request.Submit();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Request {RequestId} submitted successfully. Queuing validation job...", id);
            await _queue.EnqueueJobAsync(request.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Submit validation failed for request {RequestId}", id);
            throw;
        }
    }

    public async Task<ReplenishmentRequest> ApproveRequestAsync(Guid id, string? reviewerName)
    {
        _logger.LogInformation("Attempting to approve request {RequestId} by reviewer {ReviewerName}", id, reviewerName);

        var request = await _context.Requests.FindAsync(id);
        if (request == null)
        {
            _logger.LogWarning("Approval failed: Replenishment request {RequestId} not found", id);
            throw new KeyNotFoundException($"Replenishment request with ID {id} not found.");
        }

        try
        {
            request.Approve(reviewerName);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Request {RequestId} approved successfully by reviewer {ReviewerName}", id, reviewerName);
            return request;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Approval validation failed for request {RequestId}", id);
            throw;
        }
    }

    public async Task<ReplenishmentRequest> RejectRequestAsync(Guid id, string? reviewerName, string reason)
    {
        _logger.LogInformation("Attempting to reject request {RequestId} by reviewer {ReviewerName}. Reason: {RejectionReason}", id, reviewerName, reason);

        var request = await _context.Requests.FindAsync(id);
        if (request == null)
        {
            _logger.LogWarning("Rejection failed: Replenishment request {RequestId} not found", id);
            throw new KeyNotFoundException($"Replenishment request with ID {id} not found.");
        }

        try
        {
            request.Reject(reviewerName, reason);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Request {RequestId} rejected successfully by reviewer {ReviewerName}", id, reviewerName);
            return request;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Rejection validation failed for request {RequestId}", id);
            throw;
        }
    }

    public async Task<ReplenishmentRequest> FulfillRequestAsync(Guid id, Dictionary<Guid, int> fulfilledQuantities)
    {
        _logger.LogInformation("Attempting to fulfill request {RequestId}", id);

        var request = await _context.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (request == null)
        {
            _logger.LogWarning("Fulfillment failed: Replenishment request {RequestId} not found", id);
            throw new KeyNotFoundException($"Replenishment request with ID {id} not found.");
        }

        try
        {
            request.Fulfill(fulfilledQuantities);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Request {RequestId} fulfilled successfully", id);
            return request;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Fulfillment validation failed for request {RequestId}", id);
            throw;
        }
    }
}
