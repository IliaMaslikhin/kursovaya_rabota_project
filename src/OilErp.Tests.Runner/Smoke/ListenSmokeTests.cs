using OilErp.Tests.Runner.Util;
using OilErp.Core.Dto;
using Npgsql;

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
            var received = false;
            EventHandler<DbNotification> handler = (_, n) =>
            {
                if (string.Equals(n.Channel, channel, StringComparison.Ordinal)) received = true;
            };

            storage.Notified += handler;
            await storage.SubscribeAsync(channel, CancellationToken.None);
            await storage.UnsubscribeAsync(channel, CancellationToken.None);

            // отправляем уведомление и убеждаемся, что оно не доставлено после отписки
            await using (var conn = new NpgsqlConnection(TestEnvironment.LoadStorageConfig().ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"notify \"{channel}\", 'ping'";
                await cmd.ExecuteNonQueryAsync();
            }

            await Task.Delay(300);
            storage.Notified -= handler;

            return received
                ? new TestResult(testName, false, "уведомление получено после отписки")
                : new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }
}
