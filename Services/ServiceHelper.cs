using Microsoft.Extensions.DependencyInjection;

namespace AIAgent.Services;

public static class ServiceHelper
{
    public static IServiceProvider Services { get; set; } = default!;

    public static T GetService<T>() where T : notnull
    {
        return Services.GetRequiredService<T>();
    }
}
