using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Core.DTOs;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Mets.Replenishment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequestsController : ControllerBase
{
    private readonly IReplenishmentService _replenishmentService;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(IReplenishmentService replenishmentService, ILogger<RequestsController> _logger)
    {
        this._replenishmentService = replenishmentService;
        this._logger = _logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequests([FromQuery] string? status, [FromQuery] string? priority, [FromQuery] string? location)
    {
        var result = await _replenishmentService.GetRequestsAsync(status, priority, location);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRequest(Guid id)
    {
        var request = await _replenishmentService.GetRequestByIdAsync(id);
        if (request == null) return NotFound();
        return Ok(request);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] ReplenishmentRequest request, [FromServices] IValidator<ReplenishmentRequest>? validator = null)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
                _logger.LogWarning("Request creation validation failed with {ErrorCount} errors: {Errors}", validationResult.Errors.Count, errors);
                return BadRequest(validationResult.ToDictionary());
            }
        }

        var created = await _replenishmentService.CreateDraftAsync(request);
        return CreatedAtAction(nameof(GetRequest), new { id = created.Id }, created);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitRequest(Guid id)
    {
        await _replenishmentService.SubmitRequestAsync(id);
        return Accepted(new { Message = "Request submitted. Validation in progress." });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveRequestDto payload, [FromServices] IValidator<ApproveRequestDto>? validator = null)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(payload);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Approve request validation failed for request {RequestId} with {ErrorCount} errors", id, validationResult.Errors.Count);
                return BadRequest(validationResult.ToDictionary());
            }
        }

        var request = await _replenishmentService.ApproveRequestAsync(id, payload.ReviewerName);
        return Ok(request);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectRequestDto payload, [FromServices] IValidator<RejectRequestDto>? validator = null)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(payload);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Reject request validation failed for request {RequestId} with {ErrorCount} errors", id, validationResult.Errors.Count);
                return BadRequest(validationResult.ToDictionary());
            }
        }

        var request = await _replenishmentService.RejectRequestAsync(id, payload.ReviewerName, payload.Reason);
        return Ok(request);
    }

    [HttpPost("{id}/fulfill")]
    public async Task<IActionResult> FulfillRequest(Guid id, [FromBody] FulfillRequestDto payload, [FromServices] IValidator<FulfillRequestDto>? validator = null)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(payload);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Fulfill request validation failed for request {RequestId} with {ErrorCount} errors", id, validationResult.Errors.Count);
                return BadRequest(validationResult.ToDictionary());
            }
        }

        var request = await _replenishmentService.FulfillRequestAsync(id, payload.FulfilledQuantities);
        return Ok(request);
    }
}
