using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 days, well within the quota.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public void Create_ExceedingRemainingQuota_FailsEvenThoughSingleRequestFitsAnnualQuota()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 10 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Employee already has 8 approved vacation days this year.
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 8),
            Days = 8,
            Status = LeaveStatus.Approved
        });
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 more days — fits the annual quota alone (10), but not the remaining balance (2).
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(db.LeaveRequests); // only the pre-existing approved request
    }

    [Fact]
    public void Approve_PendingRequest_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 10 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var pending = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pending);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(pending.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(LeaveStatus.Approved, db.LeaveRequests.Single().Status);
    }

    [Fact]
    public void Approve_NonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        using var db = _database.NewDb();
        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(id: 999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Approve_AlreadyApprovedRequest_ReturnsConflict()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 10 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var approved = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approved);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(approved.Id);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void Approve_WouldExceedQuota_ReturnsConflictAndLeavesRequestPending()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 10 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Another request for the same employee already got approved in the meantime
        // (e.g. a concurrent approval), consuming most of the quota.
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 8),
            Days = 8,
            Status = LeaveStatus.Approved
        });

        var pending = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pending);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act
        var result = controller.Approve(pending.Id);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(LeaveStatus.Pending, db.LeaveRequests.Single(r => r.Id == pending.Id).Status);
    }
}
