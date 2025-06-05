using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.SSDIndicator;

public sealed class SSDIndicatorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _icSsdSleep;
    private float _icSsdSleepTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SSDIndicatorComponent, MapInitEvent>(OnMapInit);

        _cfg.OnValueChanged(CCVars.ICSSDSleep, v => _icSsdSleep = v, true);
        _cfg.OnValueChanged(CCVars.ICSSDSleepTime, v => _icSsdSleepTime = v, true);
    }

    private void OnPlayerAttached(EntityUid uid, SSDIndicatorComponent comp, PlayerAttachedEvent args)
    {
        comp.IsSSD = false;

        if (_icSsdSleep)
        {
            // Cancel any pending sleep and remove ForcedSleeping if we added it.
            comp.FallAsleepTime = TimeSpan.Zero;

            if (comp.ForcedSleepAdded)
            {
                EntityManager.RemoveComponent<ForcedSleepingComponent>(uid);
                comp.ForcedSleepAdded = false;
            }
        }

        Dirty(uid, comp);
    }

    private void OnPlayerDetached(EntityUid uid, SSDIndicatorComponent comp, PlayerDetachedEvent args)
    {
        comp.IsSSD = true;

        if (_icSsdSleep)
        {
            comp.FallAsleepTime = comp.ShouldSleep
                ? _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime)
                : TimeSpan.Zero; // disable sleeping for entities that must stay awake
        }

        Dirty(uid, comp);
    }

    private void OnMapInit(EntityUid uid, SSDIndicatorComponent comp, MapInitEvent args)
    {
        if (_icSsdSleep && comp.IsSSD && comp.FallAsleepTime == TimeSpan.Zero && comp.ShouldSleep)
        {
            comp.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_icSsdSleep)
            return;

        var query = EntityQueryEnumerator<SSDIndicatorComponent>();
        var now = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp))
        {
            // Quick rejects – avoids extra checks every frame.
            if (!comp.IsSSD || !comp.ShouldSleep)
                continue;

            if (comp.FallAsleepTime > now)
                continue;

            if (TerminatingOrDeleted(uid) || HasComp<ForcedSleepingComponent>(uid))
                continue;

            EnsureComp<ForcedSleepingComponent>(uid);
            comp.ForcedSleepAdded = true;
        }
    }
}
