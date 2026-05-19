using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.Enums;
using Mets.Replenishment.Infrastructure.Data;
using Mets.Replenishment.Api.Background;

namespace Mets.Replenishment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequestsController : ControllerBase
{
    private readonly ReplenishmentDbContext _context;
    private readonly ValidationJobQueue _queue;

    public RequestsController(ReplenishmentDbContext context, ValidationJobQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequests([FromQuery] string? status, [FromQuery] string? priority, [FromQuery] string? location)
    {
        var query = _context.Requests.Include(r => r.Items).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<RequestPriority>(priority, true, out var parsedPriority))
            query = query.Where(r => r.Priority == parsedPriority);
            
        if (!string.IsNullOrEmpty(location))
            query = query.Where(r => r.Location.Contains(location));

        var result = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRequest(Guid id)
    {
        var request = await _context.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        return Ok(request);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] ReplenishmentRequest request)
    {
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
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRequest), new { id = request.Id }, request);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitRequest(Guid id)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Draft)
            return BadRequest("Only draft requests can be submitted.");

        request.Status = RequestStatus.Submitted;
        request.ValidationStatus = ValidationStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Queue validation job
        await _queue.EnqueueJobAsync(request.Id);

        return Accepted(new { Message = "Request submitted. Validation in progress." });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Submitted || request.ValidationStatus != ValidationStatus.Completed)
            return BadRequest("Request must be submitted and validated before approval.");

        request.Status = RequestStatus.Approved;
        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    public class RejectPayload { public string Reason { get; set; } = string.Empty; }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectPayload payload)
    {
        var request = await _context.Requests.FindAsync(id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Submitted)
            return BadRequest("Request must be submitted before rejection.");

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = payload.Reason;
        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPost("{id}/fulfill")]
    public async Task<IActionResult> FulfillRequest(Guid id, [FromBody] System.Collections.Generic.Dictionary<Guid, int> fulfilledQuantities)
    {
        var request = await _context.Requests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Approved)
            return BadRequest("Only approved requests can be fulfilled.");

        foreach (var item in request.Items)
        {
            if (fulfilledQuantities.TryGetValue(item.Id, out var qty))
            {
                item.FulfilledQuantity = qty;
            }
        }

        request.Status = RequestStatus.Fulfilled;
        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(request);
    }
}
