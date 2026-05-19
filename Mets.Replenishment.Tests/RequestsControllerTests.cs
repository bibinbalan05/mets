using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using Mets.Replenishment.Api.Controllers;
using Mets.Replenishment.Api.Background;
using Mets.Replenishment.Core.Entities;
using Mets.Replenishment.Core.Enums;
using Mets.Replenishment.Infrastructure.Data;
using System.Threading.Channels;

namespace Mets.Replenishment.Tests;

[TestFixture]
public class RequestsControllerTests
{
    private ReplenishmentDbContext _context = null!;
    private ValidationJobQueue _queue = null!;
    private RequestsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ReplenishmentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ReplenishmentDbContext(options);
        
        _queue = new ValidationJobQueue();
        
        _controller = new RequestsController(_context, _queue);
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
        var result = await _controller.ApproveRequest(request.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        
        var updatedRequest = await _context.Requests.FindAsync(request.Id);
        Assert.That(updatedRequest!.Status, Is.EqualTo(RequestStatus.Approved));
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

        // Act
        var result = await _controller.ApproveRequest(request.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }
}
