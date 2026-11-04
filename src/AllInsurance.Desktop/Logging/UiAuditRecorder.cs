using AllInsurance.Application.Abstractions;
using AllInsurance.Application.Authorization;
using AllInsurance.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace AllInsurance.Desktop.Logging;

internal static class UiAuditRecorder
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static void Record(string action, string area)
    {
        var session = App.Services.GetRequiredService<ISessionService>().Current;
        if (session is null) return;
        var actor = session.UserId;
        _ = Task.Run(async () =>
        {
            await Gate.WaitAsync();
            try
            {
                var factory = App.Services.GetRequiredService<IApplicationDbContextFactory>();
                await using var db = await factory.CreateDbContextAsync();
                db.SecurityEvents.Add(new SecurityEvent { Id = Guid.NewGuid(), UserId = actor,
                    EventType = action, Outcome = "SUCCESS", CreatedAtUtc = DateTime.UtcNow,
                    Details = "area=" + area[..Math.Min(area.Length, 180)] });
                await db.SaveChangesAsync();
            }
            catch (Exception) { /* Logging cannot prevent the user's action. */ }
            finally { Gate.Release(); }
        });
    }
}
