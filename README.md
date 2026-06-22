# Snitch example + modder API

A minimal example of integrating with the [**Snitch**](https://github.com/DooDesch-Mods/ScheduleOne-Snitch)
performance profiler for Schedule I. Snitch lets your mod report its own performance - timed sections, numeric
counters, entity-state distributions, and ablation levers - and it is a **zero-overhead no-op when Snitch is
not installed**, so you can ship the integration with no hard dependency.

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de](https://support.doodesch.de).

## The basics are free

When Snitch is installed and sampling, it **auto-times every loaded mod's per-frame methods** (`OnUpdate`,
`OnFixedUpdate`, `OnLateUpdate`, `OnGUI`) and shows them as `<YourMod>.OnUpdate` - with **no code on your
side**. Your mod already appears in the frame budget. Add the API below only to go further.

## Two ways to add the API

- **Copy-in source (recommended):** drop [`Snitch.Api/Snitch.cs`](Snitch.Api/Snitch.cs) into your mod
  project. It compiles into your DLL - nothing extra to ship.
- **Reference the DLL:** build `Snitch.Api/Snitch.Api.csproj` and reference `Snitch.Api.dll`.

Both bind to the running Snitch host purely by reflection, so they share no type with it and work regardless
of load order.

## Register counters + state with zero wiring

Name a class `SnitchProbe` with a static `Register()` - Snitch **discovers and calls it automatically** when
sampling starts, so nothing goes into your `OnInitializeMelon`. See
[`SnitchExample/Core.cs`](SnitchExample/Core.cs):

```csharp
using Snitch.Api;   // Profiler, StateSnapshot

internal static class SnitchProbe
{
    public static void Register()
    {
        Profiler.RegisterCounter("MyMod.QueueLength", () => MyMod.Queue.Count, "items");
        Profiler.RegisterStateProvider("MyMod.Jobs", () =>
            new StateSnapshot { Title = "Jobs" }.Add("running", MyMod.Running).Add("queued", MyMod.Queued));
    }
}
```

## The full API

```csharp
using Snitch.Api;   // Profiler, StateSnapshot, Scope

// 1) Hand-time a sub-section (finer than the automatic per-mod timing). No heap alloc; no-op when not sampling.
using (Profiler.Sample("MyMod.Pathfinding")) { ...expensive work... }

// gate hot loops for the absolutely-free path:
if (Profiler.Enabled) using (Profiler.Sample("MyMod.Tick")) { ... }

// 2) A numeric gauge (polled a few Hz by the host)
Profiler.RegisterCounter("MyMod.QueueLength", () => _queue.Count, "items");

// 3) An entity/state distribution (shown as a bar panel in the HUD + web dashboard)
Profiler.RegisterStateProvider("MyMod.Jobs", () =>
    new StateSnapshot { Title = "Jobs" }.Add("running", _running).Add("queued", _queued));

// 4) An ablation lever so 'snitch ablate mymod' measures your subsystem's causal frame cost
Profiler.RegisterAblationLever("mymod.particles", apply: () => DisableParticles(), restore: () => EnableParticles());

// 5) Mark a one-off spike
Profiler.Mark("MyMod.LevelLoaded");
```

**Rules:** call from the Unity main thread. Prefix your labels with `MyMod.` so they roll up per mod in the
UI. Counter/state delegates are invoked by the host on the main thread, so they may touch game objects.

## See your data

In game: `snitch start`, then `snitch hud on` (or `snitch top` / `snitch states` / `snitch counters`). In the
browser: open the Snitch web dashboard - it auto-connects and shows your sections, counters and states live
alongside the vanilla NPC/trash/quest data.

## Building this example

The `.csproj` files reference MelonLoader from a local `Workspace/lib` (the author's build layout) and copy
to the game's `Mods/` folder on build - adjust those paths to your own setup. You only really need
`Snitch.Api/Snitch.cs` and `SnitchExample/Core.cs` to learn the API.

## License

MIT.
