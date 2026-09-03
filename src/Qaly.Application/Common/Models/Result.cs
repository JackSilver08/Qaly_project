using System.Diagnostics;

namespace Qaly.Application.Common.Models;

/// <summary>
/// Bộ bao kết quả dùng chung cho tất cả thao tác.
/// Thay vì ném exception, service trả về Result<T>.
/// </summary>
public class Result<T>
{
    internal Result(
        bool isSuccess,
        T? data,
        string? error,
        int statusCode,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Metadata = metadata;
        TraceId = Activity.Current?.Id;
    }

    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }
    public string? TraceId { get; }

    public static implicit operator Result<T>(Result result)
        => new(result.IsSuccess, default, result.Error, result.StatusCode, result.ErrorCode, result.Metadata);
}

public class Result
{
    private Result(
        bool isSuccess,
        string? error,
        int statusCode,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Metadata = metadata;
        TraceId = Activity.Current?.Id;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }
    public string? TraceId { get; }

    public static Result<T> Success<T>(T data)
        => new(true, data, null, 200);

    public static Result<T> Created<T>(T data)
        => new(true, data, null, 201);

    public static Result<T> Accepted<T>(T data, IReadOnlyDictionary<string, object?>? metadata = null)
        => new(true, data, null, 202, metadata: metadata);

    public static Result<T> Failure<T>(
        string error,
        int statusCode = 400,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(false, default, error, statusCode, errorCode, metadata);

    public static Result<T> Failure<T>(
        T data,
        string error,
        int statusCode,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(false, data, error, statusCode, errorCode, metadata);

    public static Result<T> Conflict<T>(T data, string message = "Resource was modified by another request.")
        => new(false, data, message, 409);

    public static Result<T> NotFound<T>(string message = "Không tìm thấy tài nguyên")
        => new(false, default, message, 404);

    public static Result<T> Forbidden<T>(string message = "Không có quyền truy cập")
        => new(false, default, message, 403);

    public static Result Success()
        => new(true, null, 200);

    public static Result Failure(
        string error,
        int statusCode = 400,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(false, error, statusCode, errorCode, metadata);

    public static Result NotFound(string message = "Không tìm thấy tài nguyên")
        => new(false, message, 404);

    public static Result Forbidden(string message = "Không có quyền truy cập")
        => new(false, message, 403);
}
