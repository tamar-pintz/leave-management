using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

// NOTE: This controller was written quickly for a POC.
// It does data access, business logic and validation all in one place.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;

    public LeaveRequestsController(LeaveDbContext db)
    {
        _db = db;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        var all = _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
        return Ok(all);
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        // Build a quick query to filter by the employee name.
        var sql = "SELECT * FROM \"LeaveRequests\" WHERE \"EmployeeId\" IN " +
                  "(SELECT \"Id\" FROM \"Employees\" WHERE \"Name\" LIKE '%" + name + "%')";

        var results = _db.LeaveRequests
            .FromSqlRaw(sql)
            .ToList();

        return Ok(results);
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound("Employee not found");

        var days = (dto.EndDate - dto.StartDate).Days + 1;

        // How many vacation days has the employee already used this year?
        var used = _db.LeaveRequests
            .Where(r => r.EmployeeId == dto.EmployeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status == LeaveStatus.Approved)
            .Sum(r => r.Days);

        // Make sure the request does not exceed the quota, accounting for days already approved.
        if (dto.Type == LeaveType.Vacation && used + days > employee.AnnualQuota)
        {
            return BadRequest("Not enough vacation balance");
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        _db.SaveChanges();

        return Ok(request);
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id)
    {
        using var transaction = _db.Database.BeginTransaction();

        var request = _db.LeaveRequests.FirstOrDefault(r => r.Id == id);
        if (request == null)
            return NotFound("Leave request not found");

        if (request.Status != LeaveStatus.Pending)
            return Conflict($"Leave request is already {request.Status}");

        // On PostgreSQL, lock the employee row so two concurrent approvals for the
        // same employee can't both read a stale "used" sum and both pass the quota
        // check. SQLite has no row-level locking (and no FOR UPDATE support); it
        // takes a whole-database write lock per transaction instead, which happens
        // to serialize this too, but only because of its single-writer model, not
        // because we asked for a row lock. See DECISIONS.md for more.
        var employee = _db.Database.IsNpgsql()
            ? _db.Employees.FromSqlInterpolated($"SELECT * FROM \"Employees\" WHERE \"Id\" = {request.EmployeeId} FOR UPDATE").FirstOrDefault()
            : _db.Employees.FirstOrDefault(e => e.Id == request.EmployeeId);

        if (employee == null)
            return NotFound("Employee not found");

        if (request.Type == LeaveType.Vacation)
        {
            var used = _db.LeaveRequests
                .Where(r => r.EmployeeId == request.EmployeeId
                            && r.Id != request.Id
                            && r.Type == LeaveType.Vacation
                            && r.Status == LeaveStatus.Approved)
                .Sum(r => r.Days);

            if (used + request.Days > employee.AnnualQuota)
            {
                transaction.Rollback();
                return Conflict("Approving this request would exceed the employee's annual vacation quota");
            }
        }

        request.Status = LeaveStatus.Approved;
        _db.SaveChanges();
        transaction.Commit();

        return Ok(request);
    }
}
