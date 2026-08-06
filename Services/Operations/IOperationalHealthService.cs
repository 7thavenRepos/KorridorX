using KorridorX.Dtos.Operations;

namespace KorridorX.Services.Operations;

public interface IOperationalHealthService
{
    Task<OperationalHealthDto> GetAsync(CancellationToken ct = default);
}
