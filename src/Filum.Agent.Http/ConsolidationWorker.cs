using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Agent.Http;

/// <summary>
/// The background passes of consolidation (spec 030), when <c>Consolidation:Enabled</c>: every minute it looks for the
/// people whose last message is older than <see cref="ConsolidationOptions.QuietMinutes"/> and consolidates them; in
/// the nightly hour it also takes whoever has anything pending. It keeps no state that matters: a pass with nothing
/// pending does nothing, and the per-person lock lets several instances of the host run it side by side.
/// </summary>
public sealed class ConsolidationWorker(IServiceScopeFactory scopes, IOptions<ConsolidationOptions> options, ILogger<ConsolidationWorker> logger, TimeProvider? time = null) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    /// <summary>A person with nothing said for this long is not looked at by the quiet passes (the nightly one still is).</summary>
    private static readonly TimeSpan Recent = TimeSpan.FromDays(2);

    private DateOnly _lastNight;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var clock = time ?? TimeProvider.System;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PassAsync(clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("[ ConsolidationWorker ] A round failed: {ExceptionType}", exception.GetType().Name);
            }

            await Task.Delay(Tick, clock, stoppingToken);
        }
    }

    /// <summary>One round: the people due now, each consolidated in its own scope.</summary>
    public async Task<int> PassAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nightly = now.Hour == options.Value.NightlyHourUtc && DateOnly.FromDateTime(now.UtcDateTime) != _lastNight;
        IReadOnlyList<Guid> due;
        using (var scope = scopes.CreateScope())
        {
            due = await DueAsync(scope.ServiceProvider.GetRequiredService<DbContext>(), now, nightly, cancellationToken);
        }

        foreach (var person in due)
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ConsolidationService>().RunAsync(person, cancellationToken);
        }

        if (nightly)
        {
            _lastNight = DateOnly.FromDateTime(now.UtcDateTime);
        }

        return due.Count;
    }

    private async Task<IReadOnlyList<Guid>> DueAsync(DbContext db, DateTimeOffset now, bool nightly, CancellationToken cancellationToken)
    {
        var quiet = now - TimeSpan.FromMinutes(options.Value.QuietMinutes);
        var since = nightly ? DateTimeOffset.MinValue : now - Recent;
        var spoken = MemoryEventKind.Spoken.ToList();
        var people = await db.Set<MemoryEventRow>()
            .Where(e => spoken.Contains(e.Kind) && e.RecordedAt > since)
            .GroupBy(e => e.UserId)
            .Select(g => new { Person = g.Key, Last = g.Max(e => e.Id), LastAt = g.Max(e => e.RecordedAt) })
            .Where(p => nightly || p.LastAt < quiet)
            .ToListAsync(cancellationToken);
        if (people.Count == 0)
        {
            return [];
        }

        // Already consolidated through their last message: nothing to do.
        var ids = people.Select(p => p.Person).ToList();
        var passes = await db.Set<MemoryEventRow>()
            .Where(e => ids.Contains(e.UserId) && e.Kind == MemoryEventKind.Derived && e.Source == MemoryEventSource.Consolidation && e.Text == Consolidation.PassText)
            .Select(e => new { e.UserId, e.Sources })
            .ToListAsync(cancellationToken);
        var through = passes.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.SelectMany(p => p.Sources).DefaultIfEmpty(0).Max());
        return people.Where(p => p.Last > through.GetValueOrDefault(p.Person)).Select(p => p.Person).ToList();
    }
}
