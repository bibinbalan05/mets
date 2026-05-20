using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using Mets.Replenishment.Api.Controllers;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.Enums;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Infrastructure.Data;
using Mets.Replenishment.Infrastructure.Services;
using Mets.Replenishment.Core.Validators;
using Mets.Replenishment.Core.DTOs;
using System.Threading.Channels;

namespace Mets.Replenishment.Tests;

[TestFixture]
public class RequestsControllerTests
{
    private ReplenishmentDbContext _context = null!;
    private IValidationJobQueue _queue = null!;
    private RequestsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ReplenishmentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ReplenishmentDbContext(options);
        
        _queue = new ValidationJobQueue();
        
        var service = new ReplenishmentService(_context, _queue, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReplenishmentService>.Instance);
        _controller = new RequestsController(service, Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestsController>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Test]
    public async Task CreateRequest_ShouldCreateDraftRequest()
    {
        // Arrange
        var request = new ReplenishmentRequest
        {
            Location = "Station 1",
            Priority = RequestPriority.Normal,
            Items = new List<ReplenishmentRequestItem>
            {
                new ReplenishmentRequestItem { ArticleNumber = "ART-1", Description = "Desc 1", RequestedQuantity = 10 }
            }
        };

        // Act
        var result = await _controller.CreateRequest(request);

        // Assert
        Assert.That(result, Is.InstanceOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult, Is.Not.Null);
        var createdRequest = createdResult!.Value as ReplenishmentRequest;
        Assert.That(createdRequest, Is.Not.Null);
        Assert.That(createdRequest!.Status, Is.EqualTo(RequestStatus.Draft));
        Assert.That(createdRequest.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(createdRequest.Items.First().RequestId, Is.EqualTo(createdRequest.Id));
    }

    [Test]
    public async Task SubmitRequest_ShouldChangeStatusToSubmittedAndQueueJob()
    {
        // Arrange
        var request = new ReplenishmentRequest { Location = "Station 1", Status = RequestStatus.Draft };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SubmitRequest(request.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        
        var updatedRequest = await _context.Requests.FindAsync(request.Id);
        Assert.That(updatedRequest!.Status, Is.EqualTo(RequestStatus.Submitted));
        Assert.That(updatedRequest.ValidationStatus, Is.EqualTo(ValidationStatus.Pending));
    }

    [Test]
    public async Task ApproveRequest_ShouldChangeStatusToApproved_WhenValidationIsComplete()
    {
        // Arrange
        var request = new ReplenishmentRequest 
        { 
            Location = "Station 1", 
            Status = RequestStatus.Submitted,
            ValidationStatus = ValidationStatus.Completed 
        };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.ApproveRequest(request.Id, "Reviewer Sarah");

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        
        var updatedRequest = await _context.Requests.FindAsync(request.Id);
        Assert.That(updatedRequest!.Status, Is.EqualTo(RequestStatus.Approved));
        Assert.That(updatedRequest.ReviewedBy, Is.EqualTo("Reviewer Sarah"));
    }

    [Test]
    public async Task ApproveRequest_ShouldReturnBadRequest_WhenValidationIsNotComplete()
    {
        // Arrange
        var request = new ReplenishmentRequest 
        { 
            Location = "Station 1", 
            Status = RequestStatus.Submitted,
            ValidationStatus = ValidationStatus.Pending 
        };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _controller.ApproveRequest(request.Id));
    }

    [Test]
    public async Task RejectRequest_ShouldChangeStatusToRejectedAndSetReasonAndReviewer()
    {
        // Arrange
        var request = new ReplenishmentRequest 
        { 
            Location = "Station 1", 
            Status = RequestStatus.Submitted
        };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        var payload = new RejectRequestDto
        {
            Reason = "Item not needed",
            ReviewerName = "Reviewer Sarah"
        };

        // Act
        var result = await _controller.RejectRequest(request.Id, payload);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        
        var updatedRequest = await _context.Requests.FindAsync(request.Id);
        Assert.That(updatedRequest!.Status, Is.EqualTo(RequestStatus.Rejected));
        Assert.That(updatedRequest.RejectionReason, Is.EqualTo("Item not needed"));
        Assert.That(updatedRequest.ReviewedBy, Is.EqualTo("Reviewer Sarah"));
    }

    [Test]
    public async Task CreateRequest_ShouldReturnBadRequest_WhenValidationFails()
    {
        // Arrange
        var request = new ReplenishmentRequest
        {
            Location = "", // Invalid
            CreatedBy = "Worker Alice",
            Items = new List<ReplenishmentRequestItem>
            {
                new ReplenishmentRequestItem { ArticleNumber = "INVALID-CODE", Description = "Desc 1", RequestedQuantity = 0 } // Invalid
            }
        };
        var validator = new ReplenishmentRequestValidator();

        // Act
        var result = await _controller.CreateRequest(request, validator);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        var errors = badRequestResult!.Value as IDictionary<string, string[]>;
        Assert.That(errors, Is.Not.Null);
        Assert.That(errors!.ContainsKey("Location"), Is.True);
        Assert.That(errors.ContainsKey("Items[0].ArticleNumber"), Is.True);
        Assert.That(errors.ContainsKey("Items[0].RequestedQuantity"), Is.True);
    }

    [Test]
    public async Task RejectRequest_ShouldReturnBadRequest_WhenValidationFails()
    {
        // Arrange
        var request = new ReplenishmentRequest 
        { 
            Location = "Station 1", 
            Status = RequestStatus.Submitted
        };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        var payload = new RejectRequestDto
        {
            Reason = "", // Invalid
            ReviewerName = "" // Invalid
        };
        var validator = new RejectRequestDtoValidator();

        // Act
        var result = await _controller.RejectRequest(request.Id, payload, validator);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        var errors = badRequestResult!.Value as IDictionary<string, string[]>;
        Assert.That(errors, Is.Not.Null);
        Assert.That(errors!.ContainsKey("Reason"), Is.True);
        Assert.That(errors.ContainsKey("ReviewerName"), Is.True);
    }
}
