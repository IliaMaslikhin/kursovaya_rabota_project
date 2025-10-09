namespace OilErp.Core.Dto;

/// <summary>
/// Database notification event data
/// </summary>
/// <param name="Channel">Notification channel</param>
/// <param name="Payload">Notification payload</param>
/// <param name="ProcessId">Process ID that sent the notification</param>
public record DbNotification(
    string Channel,
    string Payload,
    int ProcessId
);
