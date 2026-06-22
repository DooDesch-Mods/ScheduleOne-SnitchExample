using System;
using MelonLoader;
using Snitch.Api;   // Profiler, StateSnapshot

[assembly: MelonInfo(typeof(SnitchExample.Core), "SnitchExample", "1.0.0", "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-SnitchExample")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SnitchExample
{
    /// <summary>
    /// Minimal demonstration of the Snitch profiler's modder API. Three things light up when Snitch is installed,
    /// and every one stays a zero-overhead no-op when it isn't - this mod has NO hard dependency on Snitch:
    ///   1. PER-MOD TIMING FOR FREE - Snitch auto-times this mod's OnUpdate as "SnitchExample.OnUpdate". No code.
    ///   2. ZERO-WIRING COUNTERS/STATE - name a class "SnitchProbe" with a static Register() (see below); Snitch
    ///      discovers and calls it automatically. Nothing to wire into OnInitializeMelon.
    ///   3. OPTIONAL EXPLICIT SECTIONS - using (Profiler.Sample("Example.Work")) { ... } hand-times a sub-section.
    /// </summary>
    public sealed class Core : MelonMod
    {
        // static so the convention probe (a static class) can read them; a MelonMod is effectively a singleton.
        internal static int Tick;
        internal static int Red, Green, Blue;

        public override void OnInitializeMelon()
        {
            // Nothing to register here - SnitchProbe.Register() (below) is auto-discovered + called by the host.
            LoggerInstance.Msg("SnitchExample loaded. With Snitch installed: OnUpdate is auto-timed and " +
                "Example.Tick + Example.Widgets auto-register (no-op otherwise).");
        }

        public override void OnUpdate()
        {
            Tick++;
            Red = (Tick / 60) % 10;
            Green = (Tick / 30) % 7;
            Blue = (Tick / 90) % 5;

            // Optional: hand-time a representative chunk of work as its own section. Gated on Profiler.Enabled so it
            // is truly free when Snitch is absent or not sampling. (The whole OnUpdate is auto-timed regardless.)
            if (Profiler.Enabled)
            {
                using (Profiler.Sample("Example.Work"))
                {
                    double s = 0;
                    for (int i = 0; i < 4000; i++) s += Math.Sqrt(i * 0.5 + 1.0);
                    if (s < 0) Tick++;   // keep the loop from being optimized away
                }
            }
        }
    }

    /// <summary>
    /// Snitch convention probe. A class named "SnitchProbe" with a static Register() is auto-discovered and called
    /// by the Snitch host when sampling starts - so a mod registers its gauges/state with ZERO wiring in its Core.
    /// Registering the same id again replaces it, and every call is a no-op when Snitch is not installed.
    /// </summary>
    internal static class SnitchProbe
    {
        public static void Register()
        {
            // A numeric gauge polled by the host a few times a second.
            Profiler.RegisterCounter("Example.Tick", () => Core.Tick, "ticks");

            // A small name -> count distribution, polled on the main thread (safe to read game state here).
            Profiler.RegisterStateProvider("Example.Widgets", () =>
                new StateSnapshot { Title = "Example Widgets" }
                    .Add("red", Core.Red).Add("green", Core.Green).Add("blue", Core.Blue));
        }
    }
}
