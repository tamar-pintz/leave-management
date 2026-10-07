using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

// Thin HTTP layer: translates requests/responses, all business logic and data
// access live in ILeaveRequestService.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveRequestsController(ILeaveRequestService service)
    {
        _service = service;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_service.GetAll());
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        return Ok(_service.Search(name));
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        return ToActionResult(_service.Create(dto));
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id)
    {
        return ToActionResult(_service.Approve(id));
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.Success)
            return Ok(result.Value);

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(result.Error),
            ServiceErrorType.Conflict => Conflict(result.Error),
            ServiceErrorType.Invalid => BadRequest(result.Error),
            _ => StatusCode(500, result.Error)
        };
    }
}

