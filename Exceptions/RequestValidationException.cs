namespace KorridorX.Exceptions;

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Review the highlighted fields and try again.")
{
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; } = errors;
}
