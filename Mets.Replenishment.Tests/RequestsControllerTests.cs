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
        var result = await _controller.ApproveRequest(request.Id, new ApproveRequestDto { ReviewerName = "Reviewer Sarah" });

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
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _controller.ApproveRequest(request.Id, new ApproveRequestDto { ReviewerName = "Reviewer Sarah" }));
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

    [Test]
    public async Task GetRequests_ShouldReturnFirstPageWithCorrectSize()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            _context.Requests.Add(new ReplenishmentRequest { Location = $"Loc {i}", CreatedAt = DateTime.UtcNow.AddMinutes(i) });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(null, null, null, pageNumber: 1, pageSize: 10);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        var pagedResult = okResult!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(25));
        Assert.That(pagedResult.Items.Count(), Is.EqualTo(10));
        Assert.That(pagedResult.PageNumber, Is.EqualTo(1));
        Assert.That(pagedResult.PageSize, Is.EqualTo(10));
    }

    [Test]
    public async Task GetRequests_ShouldReturnCorrectSubsetForSecondPage()
    {
        // Arrange
        var list = new List<ReplenishmentRequest>();
        var now = DateTime.UtcNow;
        for (int i = 1; i <= 15; i++)
        {
            list.Add(new ReplenishmentRequest { Location = $"Loc {i}", CreatedAt = now.AddMinutes(i) });
        }
        _context.Requests.AddRange(list);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(null, null, null, pageNumber: 2, pageSize: 5);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(15));
        
        var items = pagedResult.Items.ToList();
        Assert.That(items.Count, Is.EqualTo(5));
        Assert.That(items[0].Location, Is.EqualTo("Loc 10"));
        Assert.That(items[4].Location, Is.EqualTo("Loc 6"));
    }

    [Test]
    public async Task GetRequests_ShouldFilterByStatus()
    {
        // Arrange
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc A", Status = RequestStatus.Draft });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc B", Status = RequestStatus.Submitted });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc C", Status = RequestStatus.Draft });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(status: "Draft", null, null, pageNumber: 1, pageSize: 10);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var pagedResult = (result as OkObjectResult)!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(2));
        Assert.That(pagedResult.Items.All(r => r.Status == RequestStatus.Draft), Is.True);
    }

    [Test]
    public async Task GetRequests_ShouldFilterByPriority()
    {
        // Arrange
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc A", Priority = RequestPriority.Urgent });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc B", Priority = RequestPriority.Normal });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Loc C", Priority = RequestPriority.Urgent });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(null, priority: "Urgent", null, pageNumber: 1, pageSize: 10);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var pagedResult = (result as OkObjectResult)!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(2));
        Assert.That(pagedResult.Items.All(r => r.Priority == RequestPriority.Urgent), Is.True);
    }

    [Test]
    public async Task GetRequests_ShouldFilterByLocation()
    {
        // Arrange
        _context.Requests.Add(new ReplenishmentRequest { Location = "Warehouse 1" });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Station 2" });
        _context.Requests.Add(new ReplenishmentRequest { Location = "Warehouse 3" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(null, null, location: "Warehouse", pageNumber: 1, pageSize: 10);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var pagedResult = (result as OkObjectResult)!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(2));
        Assert.That(pagedResult.Items.All(r => r.Location.Contains("Warehouse")), Is.True);
    }

    [Test]
    public async Task GetRequests_ShouldReturnEmptyList_WhenPageIsOutOfBounds()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            _context.Requests.Add(new ReplenishmentRequest { Location = $"Loc {i}" });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetRequests(null, null, null, pageNumber: 10, pageSize: 5);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var pagedResult = (result as OkObjectResult)!.Value as PagedResult<ReplenishmentRequest>;
        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.TotalCount, Is.EqualTo(5));
        Assert.That(pagedResult.Items, Is.Empty);
    }
}
