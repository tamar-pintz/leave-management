using LeaveManagement.Api.Models;

namespace LeaveManagement.Api.Services;

public interface ILeaveRequestService
{
    List<LeaveRequestDto> GetAll();

    List<LeaveRequestDto> Search(string name);

    ServiceResult<LeaveRequestDto> Create(CreateLeaveRequestDto dto);

    ServiceResult<LeaveRequestDto> Approve(int id);
}
