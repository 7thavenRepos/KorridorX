namespace KorridorX.Infrastructure;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public PageMeta? Meta { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
}

public static class ApiResponses
{
    public static ApiResponse<T> Ok<T>(T data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> OkPaged<T>(T data, PageMeta meta, string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Meta = meta
        };
    }

    public static ApiResponse<object> Fail(
        string message,
        string code = "BAD_REQUEST",
        object? details = null)
    {
        return new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
    }
}