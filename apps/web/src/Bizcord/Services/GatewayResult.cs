using System.Net;

namespace Bizcord.Services;

/// <summary>The result of a gateway call: success (maybe with a value) or an error message that can be shown.</summary>
public class GatewayResult
{
    public string? Error { get; }

    /// <summary>Null when the gateway could not be reached at all.</summary>
    public HttpStatusCode? StatusCode { get; }

    public bool IsSuccess => Error is null;

    protected GatewayResult(string? error, HttpStatusCode? statusCode)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public static GatewayResult Ok() => new(null, null);
    public static GatewayResult Fail(string error, HttpStatusCode? statusCode = null) => new(error, statusCode);
}

public class GatewayResult<T> : GatewayResult
{
    public T? Value { get; }

    private GatewayResult(T? value, string? error, HttpStatusCode? statusCode) : base(error, statusCode) => Value = value;

    public static GatewayResult<T> Ok(T value) => new(value, null, null);
    public static new GatewayResult<T> Fail(string error, HttpStatusCode? statusCode = null) => new(default, error, statusCode);
}
