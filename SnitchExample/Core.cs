using System;
using MelonLoader;
using Snitch.Api;   // Profiler, Panel, StateSnapshot

[assembly: MelonInfo(typeof(SnitchExample.Core), "SnitchExample", "1.1.0", "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-SnitchExample")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SnitchExample
{
    /// <summary>
    /// Minimal demonstration of the Snitch profiler's modder API. Everything lights up when Snitch is installed
    /// and stays a zero-overhead no-op when it isn't - this mod has NO hard dependency on Snitch:
    ///   1. PER-MOD TIMING FOR FREE - Snitch auto-times this mod's OnUpdate as "SnitchExample.OnUpdate". No code.
    ///   2. ZERO-WIRING PANEL - name a class "SnitchProbe" with a static Register() (see below); Snitch discovers
    ///      and calls it automatically. There it builds a PANEL - your own toggleable, movable, resizable area in
    ///      the Snitch overlay + web dashboard - with counters, state, free text, action buttons, toggles and a log.
    ///   3. OPTIONAL EXPLICIT SECTIONS - using (Profiler.Sample("Example.Work")) { ... } hand-times a sub-section.
    /// </summary>
    public sealed class Core : MelonMod
    {
        // static so the convention probe (a static class) can read them; a MelonMod is effectively a singleton.
        internal static int Tick;
        internal static int Red, Green, Blue;
        internal static bool BusyWork = true;   // driven by a panel Toggle below

        public override void OnInitializeMelon()
        {
            // Nothing to register here - SnitchProbe.Register() (below) is auto-discovered + called by the host.
            LoggerInstance.Msg("SnitchExample loaded. With Snitch installed it gets a panel in the overlay/dashboard " +
                "and its OnUpdate is auto-timed (all a no-op otherwise).");
        }

        public override void OnUpdate()
        {
            Tick++;
            Red = (Tick / 60) % 10;
            Green = (Tick / 30) % 7;
            Blue = (Tick / 90) % 5;

            // Optional: hand-time a representative chunk of work as its own section. Gated on Profiler.Enabled so it
            // is truly free when Snitch is absent or not sampling. The "Busy work" panel toggle flips it live.
            if (BusyWork && Profiler.Enabled)
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
    /// by the Snitch host - so a mod registers everything with ZERO wiring in its Core. Registering the same id again
    /// replaces it, and every call is a no-op when Snitch is not installed.
    /// </summary>
    internal static class SnitchProbe
    {
        public static void Register()
        {
            // Declare a panel: your mod's own toggleable, movable, resizable area in the Snitch overlay + dashboard.
            // Counters/state added through it get an id of "<panelId>.<name>" so they group under the panel.
            Panel p = Profiler.RegisterPanel("Example", "Snitch Example");

            // A numeric gauge polled by the host a few times a second (-> "Example.Tick").
            p.Counter("Tick", () => Core.Tick, "ticks");

            // A small name -> count distribution, polled on the main thread (safe to read game state here).
            p.State("Widgets", () => new StateSnapshot { Title = "Widgets" }
                .Add("red", Core.Red).Add("green", Core.Green).Add("blue", Core.Blue));

            // A free-text, multi-line readout for anything a counter/distribution can't express.
            p.Text(() => $"busy work: {(Core.BusyWork ? "on" : "off")}");

            // A clickable button (the in-game replacement for a debug hotkey). Runs on the main thread.
            p.Action("Reset tick", () => { Core.Tick = 0; p.Write("tick reset to 0"); });

            // An on/off control (the in-game replacement for a toggle hotkey).
            p.Toggle("Busy work", () => Core.BusyWork, v => Core.BusyWork = v);

            // Show this panel's own log channel (lines sent via p.Write / Profiler.Log("Example", ...)).
            p.Log();
        }
    }
}
