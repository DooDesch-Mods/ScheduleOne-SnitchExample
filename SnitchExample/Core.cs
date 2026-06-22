using System;
using MelonLoader;
using Snitch.Api;               // StateSnapshot
using Prof = Snitch.Api.Snitch; // alias avoids the Snitch namespace/type ambiguity

[assembly: MelonInfo(typeof(SnitchExample.Core), "SnitchExample", "1.0.0", "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-SnitchExample")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SnitchExample
{
    /// <summary>
    /// Minimal demonstration of the Snitch modder API. Shows the three integration kinds:
    ///   - a timed section   : using (Prof.Sample("Example.Work")) { ... }
    ///   - a numeric counter  : Prof.RegisterCounter("Example.Tick", () =&gt; _tick, "ticks")
    ///   - a state provider   : Prof.RegisterStateProvider("Example.Widgets", () =&gt; snapshot)
    /// All are no-ops when Snitch isn't installed. This mod has no hard dependency on Snitch.
    /// </summary>
    public sealed class Core : MelonMod
    {
        private int _tick;
        private int _red, _green, _blue;

        public override void OnInitializeMelon()
        {
            // Registration is load-order-proof: if Snitch loads after this mod, the shim queues and applies it.
            Prof.RegisterCounter("Example.Tick", () => _tick, "ticks");
            Prof.RegisterStateProvider("Example.Widgets", () =>
                new StateSnapshot { Title = "Example Widgets" }.Add("red", _red).Add("green", _green).Add("blue", _blue));
            LoggerInstance.Msg("SnitchExample loaded - registered Example.Tick + Example.Widgets + times Example.Work (no-op unless Snitch is installed & sampling).");
        }

        public override void OnUpdate()
        {
            _tick++;
            _red = (_tick / 60) % 10;
            _green = (_tick / 30) % 7;
            _blue = (_tick / 90) % 5;

            // Time a representative chunk of work. Gated on Prof.Enabled so it's truly free when not profiling.
            if (Prof.Enabled)
            {
                using (Prof.Sample("Example.Work"))
                {
                    double s = 0;
                    for (int i = 0; i < 4000; i++) s += Math.Sqrt(i * 0.5 + 1.0);
                    if (s < 0) _tick++;   // keep the loop from being optimized away
                }
            }
        }
    }
}
