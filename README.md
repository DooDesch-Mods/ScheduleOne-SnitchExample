# Snitch example + modder API

A minimal example of integrating with the [**Snitch**](https://github.com/DooDesch-Mods/ScheduleOne-Snitch)
performance profiler for Schedule I. Snitch lets your mod report its own performance - timed sections, numeric
counters, entity-state distributions, and ablation levers - and it is a **zero-overhead no-op when Snitch is
not installed**, so you can ship the integration with no hard dependency.

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de](https://support.doodesch.de).

## Two ways to add the API

- **Copy-in source (recommended):** drop [`Snitch.Api/Snitch.cs`](Snitch.Api/Snitch.cs) into your mod
  project. It compiles into your DLL - nothing extra to ship.
- **Reference the DLL:** build `Snitch.Api/Snitch.Api.csproj` and reference `Snitch.Api.dll`.

Both bind to the running Snitch host purely by reflection, so they share no type with it and work regardless
of load order.

## Use it

See [`SnitchExample/Core.cs`](SnitchExample/Core.cs) for the full, working example:

```csharp
using Snitch.Api;                  // StateSnapshot
using Prof = Snitch.Api.Snitch;    // alias avoids the Snitch namespace/type clash

// 1) Time a section (no heap alloc; no-op when Snitch is absent or not sampling)
using (Prof.Sample("MyMod.Pathfinding")) { ...expensive work... }

// gate hot loops for the absolutely-free path:
if (Prof.Enabled) using (Prof.Sample("MyMod.Tick")) { ... }

// 2) A numeric gauge (polled a few Hz by the host)
Prof.RegisterCounter("MyMod.QueueLength", () => _queue.Count, "items");

// 3) An entity/state distribution (shown as a bar panel in the HUD + web dashboard)
Prof.RegisterStateProvider("MyMod.Jobs", () =>
    new StateSnapshot { Title = "Jobs" }.Add("running", _running).Add("queued", _queued));

// 4) An ablation lever so 'snitch ablate mymod' measures your subsystem's causal frame cost
Prof.RegisterAblationLever("mymod.particles", apply: () => DisableParticles(), restore: () => EnableParticles());

// 5) Mark a one-off spike
Prof.Mark("MyMod.LevelLoaded");
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
