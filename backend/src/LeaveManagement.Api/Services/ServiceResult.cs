namespace LeaveManagement.Api.Services;

// Maps directly to HTTP outcomes without the service layer knowing about HTTP.
public enum ServiceErrorType
{
    NotFound,
    Conflict,
    Invalid
}

public class ServiceResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public ServiceErrorType ErrorType { get; }
    public string? Error { get; }

    private ServiceResult(bool success, T? value, ServiceErrorType errorType, string? error)
    {
        Success = success;
        Value = value;
        ErrorType = errorType;
        Error = error;
    }

    public static ServiceResult<T> Ok(T value) => new(true, value, default, null);

    public static ServiceResult<T> Fail(ServiceErrorType type, string error) => new(false, default, type, error);
}
