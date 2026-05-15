namespace SteamWorkshopAgent;

public static class ServiceLocator
{
    private static IServiceProvider? provider;

    public static void SetProvider(IServiceProvider serviceProvider) => provider = serviceProvider;

    public static T Get<T>() where T : notnull
    {
        if (provider == null)
            throw new InvalidOperationException("Service provider is not initialized.");

        var service = provider.GetService(typeof(T));
        return service is T typed
            ? typed
            : throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}
