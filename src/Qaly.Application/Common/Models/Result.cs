namespace Qaly.Application.Common.Models;

/// <summary>
/// Generic result wrapper cho tất cả operations.
/// Thay vì throw exception, service trả về Result<T>.
/// </summary>
public class Result<T>
{
    internal Result(bool isSuccess, T? data, string? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
    }

    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public int StatusCode { get; }
}

public class Result
{
    private Result(bool isSuccess, string? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    public static Result<T> Success<T>(T data)
        => new(true, data, null, 200);

    public static Result<T> Created<T>(T data)
        => new(true, data, null, 201);

    public static Result<T> Failure<T>(string error, int statusCode = 400)
        => new(false, default, error, statusCode);

    public static Result<T> NotFound<T>(string message = "Resource not found")
        => new(false, default, message, 404);

    public static Result<T> Forbidden<T>(string message = "Access denied")
        => new(false, default, message, 403);

    public static Result Success()
        => new(true, null, 200);

    public static Result Failure(string error, int statusCode = 400)
        => new(false, error, statusCode);
}
