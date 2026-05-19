using Microsoft.Extensions.DependencyInjection;

namespace Business.Helpers;

/// <summary>
/// Fire-and-forget işler için ayrı DI scope açar.
/// Aynı HTTP request scope'undaki DbContext ile paralel kullanım EF concurrency hatasına yol açar.
/// </summary>
public static class ScopedBackgroundTask
{
    public static void Run(IServiceScopeFactory scopeFactory, Func<IServiceProvider, Task> work)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(work);

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await work(scope.ServiceProvider);
            }
            catch
            {
                // Caller logs if needed; background failures must not crash the host.
            }
        });
    }
}
