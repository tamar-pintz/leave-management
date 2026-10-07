using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

// Owns data access and business rules for leave requests. The controller only
// translates between HTTP and this service.
public class LeaveRequestService : ILeaveRequestService
{
    private readonly LeaveDbContext _db;

    public LeaveRequestService(LeaveDbContext db)
    {
        _db = db;
    }

    public List<LeaveRequestDto> GetAll()
    {
        return _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList()
            .Select(ToDto)
            .ToList();
    }

    public List<LeaveRequestDto> Search(string name)
    {
        // Parameterized via EF.Functions.Like instead of raw SQL string concatenation.
        return _db.LeaveRequests
            .Include(r => r.Employee)
            .Where(r => r.Employee != null && EF.Functions.Like(r.Employee.Name, $"%{name}%"))
            .OrderByDescending(r => r.StartDate)
            .ToList()
            .Select(ToDto)
            .ToList();
    }

    public ServiceResult<LeaveRequestDto> Create(CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.NotFound, "Employee not found");

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
            return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.Invalid, "Not enough vacation balance");
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

        return ServiceResult<LeaveRequestDto>.Ok(ToDto(request, employee));
    }

    public ServiceResult<LeaveRequestDto> Approve(int id)
    {
        using var transaction = _db.Database.BeginTransaction();

        var request = _db.LeaveRequests.FirstOrDefault(r => r.Id == id);
        if (request == null)
            return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.NotFound, "Leave request not found");

        if (request.Status != LeaveStatus.Pending)
            return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.Conflict, $"Leave request is already {request.Status}");

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
            return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.NotFound, "Employee not found");

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
                return ServiceResult<LeaveRequestDto>.Fail(ServiceErrorType.Conflict, "Approving this request would exceed the employee's annual vacation quota");
            }
        }

        request.Status = LeaveStatus.Approved;
        _db.SaveChanges();
        transaction.Commit();

        return ServiceResult<LeaveRequestDto>.Ok(ToDto(request, employee));
    }

    private static LeaveRequestDto ToDto(LeaveRequest r) => ToDto(r, r.Employee);

    private static LeaveRequestDto ToDto(LeaveRequest r, Employee? employee) => new()
    {
        Id = r.Id,
        EmployeeId = r.EmployeeId,
        Employee = employee == null ? null : new EmployeeDto { Id = employee.Id, Name = employee.Name, AnnualQuota = employee.AnnualQuota },
        Type = r.Type,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        Status = r.Status,
        Days = r.Days
    };
}
