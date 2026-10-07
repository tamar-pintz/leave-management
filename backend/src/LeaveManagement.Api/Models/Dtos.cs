namespace LeaveManagement.Api.Models;

// Incoming payload for creating a leave request.
public class CreateLeaveRequestDto
{
    public int EmployeeId { get; set; }
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AnnualQuota { get; set; }
}

// Outgoing shape for a leave request. Decouples the API contract from the EF
// entity graph (no circular Employee <-> LeaveRequest navigation to serialize).
public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public EmployeeDto? Employee { get; set; }
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveStatus Status { get; set; }
    public int Days { get; set; }
}

