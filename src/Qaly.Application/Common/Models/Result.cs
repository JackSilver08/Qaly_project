namespace Qaly.Application.Common.Models;

/// <summary>
/// Generic result wrapper cho tất cả operations.
/// Thay vì throw exception, service trả về Result<T>.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? Error { get; private set; }
    public int StatusCode { get; private set; }

    private Result() { }

    public static Result<T> Success(T data)
        => new() { IsSuccess = true, Data = data, StatusCode = 200 };

    public static Result<T> Created(T data)
        => new() { IsSuccess = true, Data = data, StatusCode = 201 };

    public static Result<T> Failure(string error, int statusCode = 400)
        => new() { IsSuccess = false, Error = error, StatusCode = statusCode };

    public static Result<T> NotFound(string message = "Resource not found")
        => new() { IsSuccess = false, Error = message, StatusCode = 404 };

    public static Result<T> Forbidden(string message = "Access denied")
        => new() { IsSuccess = false, Error = message, StatusCode = 403 };
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? Error { get; private set; }
    public int StatusCode { get; private set; }

    public static Result Success()
        => new() { IsSuccess = true, StatusCode = 200 };

    public static Result Failure(string error, int statusCode = 400)
        => new() { IsSuccess = false, Error = error, StatusCode = statusCode };
}
