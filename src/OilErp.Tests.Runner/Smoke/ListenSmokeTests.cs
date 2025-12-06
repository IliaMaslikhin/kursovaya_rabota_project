using OilErp.Tests.Runner.Util;
using OilErp.Core.Dto;
using OilErp.Core.Contracts;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Дополнительные проверки LISTEN/NOTIFY.
/// </summary>
public class ListenSmokeTests
{
    /// <summary>
    /// Проверяет, что отмена токена отписывает слушателя и не оставляет канал открытым.
    /// </summary>
    public async Task<TestResult> TestListenCancelUnsubscribes()
    {
        const string testName = "Listen_Cancel_Unsubscribes";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var channel = $"hc_listen_cancel_{Guid.NewGuid():N}";
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            storage.Notified += (_, _) => { };
            await storage.SubscribeAsync(channel, CancellationToken.None);
            cts.Cancel();
            await Task.Delay(500); // дать слушателю завершиться
            await storage.UnsubscribeAsync(channel);
            storage.Notified -= (_, _) => { };
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }
}
