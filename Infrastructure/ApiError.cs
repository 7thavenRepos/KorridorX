namespace KorridorX.Infrastructure;

public class ApiError
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public object? Details { get; set; }
}