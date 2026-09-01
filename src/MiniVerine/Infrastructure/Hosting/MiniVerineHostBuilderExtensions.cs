using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MiniVerine;

public static class MiniVerineHostBuilderExtensions
{
    public static IHostApplicationBuilder UseMiniVerine(
        this IHostApplicationBuilder builder,
        Action<MiniVerineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new MiniVerineOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        return builder;
    }
}
