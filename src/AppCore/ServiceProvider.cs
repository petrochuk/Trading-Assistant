using Microsoft.Extensions.DependencyInjection;

namespace AppCore;

public static class ServiceProvider
{
    static Microsoft.Extensions.DependencyInjection.ServiceProvider _instance = null!;

    public static void Build(IServiceCollection serviceCollection) {
        _instance = serviceCollection.BuildServiceProvider();
    }

    public static Microsoft.Extensions.DependencyInjection.ServiceProvider Instance {
        get { return _instance; }
    }
}
