using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KorridorX.Tests.Infrastructure;

public sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "KorridorX.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
