using System;
using System.Collections.Generic;
using System.Reflection;

namespace Snitch.Api
{
    /// <summary>
    /// The Snitch profiler's modder API. Reference Snitch.Api.dll OR drop this single file into your mod.
    /// Every call is a zero-overhead no-op when the Snitch profiler is not installed, and lights up
    /// automatically when it is - so you can ship this unconditionally with no hard dependency.
    ///
    /// <code>
    ///   using Snitch.Api;
    ///   using (Profiler.Sample("MyMod.Pathfinding")) { ...expensive work... }   // times a section
    ///   Profiler.RegisterCounter("MyMod.QueueLen", () =&gt; _queue.Count, "items");
    ///   Profiler.RegisterStateProvider("MyMod.Jobs", () =&gt; new StateSnapshot { Title = "Jobs" }
    ///       .Add("running", _running).Add("queued", _queued));
    /// </code>
    ///
    /// Tip: a class named <c>SnitchProbe</c> with a static <c>Register()</c> is auto-discovered and called on
    /// bind (see <see cref="AutoRegister"/>), so your mod's Core doesn't need to wire anything. Snitch also
    /// auto-times every mod's OnUpdate etc., so basic per-mod frame cost needs no code at all.
    ///
    /// All calls MUST be made from the Unity main thread. Counter/state delegates are invoked by the host on
    /// the main thread, so they may safely touch game objects.
    /// </summary>
    public static class Profiler
    {
        // bound bridge delegates (null until the host is found)
        private static bool _bound;
        private static bool _autoDone;
        private static int _probeAttempts;
        private static readonly List<Action> _pending = new List<Action>();
        private static Func<bool> _isEnabled;
        private static Func<string, int> _beginScope;
        private static Action<int> _endScope;
        private static Action<string> _beginLabel;
        private static Action<string> _endLabel;
        private static Action<string, Func<double>, string> _registerCounter;
        private static Action<string> _unregisterCounter;
        private static Action<string, Func<object[]>> _registerState;
        private static Action<string> _unregisterState;
        private static Action<string> _mark;
        private static Action<string, Action, Action> _registerLever;

        /// <summary>True only when the Snitch host is installed AND sampling is currently armed. Gate hot loops
        /// on this for the absolutely-free path: <c>if (Profiler.Enabled) using (Profiler.Sample("X")) { ... }</c>.</summary>
        public static bool Enabled
        {
            get { EnsureBound(); return _isEnabled != null && _isEnabled(); }
        }

        /// <summary>Time a section. <c>using (Profiler.Sample("MyMod.Foo")) { ... }</c>. No heap allocation; a
        /// no-op (default scope) when the host is absent or sampling is off.</summary>
        public static Scope Sample(string label)
        {
            EnsureBound();
            if (_beginScope == null) return default;
            return new Scope(_beginScope(label));
        }

        /// <summary>Manual section begin (pair with <see cref="End"/>). Prefer <see cref="Sample"/>.</summary>
        public static void Begin(string label) { EnsureBound(); _beginLabel?.Invoke(label); }
        public static void End(string label) { EnsureBound(); _endLabel?.Invoke(label); }

        /// <summary>Register a numeric gauge polled by the host at a few Hz. Re-registering the same id replaces
        /// it. Load-order-proof: if Snitch loads AFTER your mod, the registration is queued and applied on bind.</summary>
        public static void RegisterCounter(string id, Func<double> read, string unit = null)
        {
            if (read == null) return;
            EnsureBound();
            if (_registerCounter != null) _registerCounter(id, read, unit ?? "");
            else _pending.Add(() => _registerCounter?.Invoke(id, read, unit ?? ""));
        }
        public static void UnregisterCounter(string id) { EnsureBound(); _unregisterCounter?.Invoke(id); }

        /// <summary>Register an entity/state-distribution snapshot, polled by the host at a few Hz on the main
        /// thread. Load-order-proof (queued until the host binds).</summary>
        public static void RegisterStateProvider(string id, Func<StateSnapshot> snapshot)
        {
            if (snapshot == null) return;
            // marshal the rich snapshot down to primitives so the shim shares no type with the host
            Func<object[]> poll = () =>
            {
                StateSnapshot s = snapshot();
                if (s == null) return new object[] { id, new string[0], new int[0], 0 };
                int n = s.Buckets.Count;
                var names = new string[n];
                var counts = new int[n];
                for (int i = 0; i < n; i++) { names[i] = s.Buckets[i].Name; counts[i] = s.Buckets[i].Count; }
                return new object[] { s.Title ?? id, names, counts, s.Total };
            };
            EnsureBound();
            if (_registerState != null) _registerState(id, poll);
            else _pending.Add(() => _registerState?.Invoke(id, poll));
        }
        public static void UnregisterStateProvider(string id) { EnsureBound(); _unregisterState?.Invoke(id); }

        /// <summary>Annotate a one-off event/spike in the timeline.</summary>
        public static void Mark(string label) { EnsureBound(); _mark?.Invoke(label); }

        /// <summary>Register an ablation lever for your subsystem so 'snitch ablate &lt;name&gt;' can measure its
        /// causal frame-time cost. <paramref name="apply"/> turns your subsystem OFF, <paramref name="restore"/>
        /// turns it back ON. Both run on the main thread. Load-order-proof (queued until the host binds).</summary>
        public static void RegisterAblationLever(string name, Action apply, Action restore)
        {
            if (string.IsNullOrEmpty(name) || apply == null) return;
            EnsureBound();
            if (_registerLever != null) _registerLever(name, apply, restore);
            else _pending.Add(() => _registerLever?.Invoke(name, apply, restore));
        }

        /// <summary>Discover a convention type named <c>SnitchProbe</c> with a static <c>Register()</c> in THIS
        /// mod's own assembly and invoke it once - so a mod never has to wire a Register() call into its Core.
        /// Drive it from a <c>[ModuleInitializer]</c> in your probe file. No-op + load-order-proof: discovery is
        /// deferred until the host binds, and is a permanent no-op if Snitch is not installed.</summary>
        public static void AutoRegister()
        {
            EnsureBound();
            if (_bound) RunAutoRegister();   // else: the bind flush will run it (load-order-proof, both directions)
        }

        private static void RunAutoRegister()
        {
            if (_autoDone) return;
            _autoDone = true;   // latch before invoking so a throw can't loop
            try
            {
                Assembly self = typeof(Profiler).Assembly;   // only this mod's assembly - single, fast, no AppDomain scan
                Type probe = self.GetType("SnitchProbe", false) ?? FindByLeafName(self, "SnitchProbe");
                MethodInfo reg = probe?.GetMethod("Register",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                reg?.Invoke(null, null);
            }
            catch { /* a mod's probe threw -> stays a no-op, never crashes the mod */ }
        }

        private static Type FindByLeafName(Assembly asm, string leaf)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            catch { return null; }
            if (types == null) return null;
            foreach (Type t in types)
                if (t != null && t.IsClass && t.IsAbstract && t.IsSealed && t.Name == leaf) return t;   // static class
            return null;
        }

        // ----- reflection handshake (runs until it binds, then latches) -----

        private static void EnsureBound()
        {
            if (_bound) return;   // bound once, never probe again (fast path)
            try
            {
                // Cheap assembly-qualified lookup every call; the expensive AppDomain scan only occasionally,
                // so a mod that calls the API every frame while Snitch is absent doesn't scan 100+ assemblies/frame.
                Type t = FindBridge((_probeAttempts++ % 30) == 0);
                if (t == null) return;   // host not present yet - cheap re-probe next call (load-order proof)
                object abi = t.GetField("AbiVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (abi is int v && v < 1) return;

                _isEnabled = Get<Func<bool>>(t, "IsEnabled");
                _beginScope = Get<Func<string, int>>(t, "BeginScope");
                _endScope = Get<Action<int>>(t, "EndScope");
                _beginLabel = Get<Action<string>>(t, "BeginLabel");
                _endLabel = Get<Action<string>>(t, "EndLabel");
                _registerCounter = Get<Action<string, Func<double>, string>>(t, "RegisterCounter");
                _unregisterCounter = Get<Action<string>>(t, "UnregisterCounter");
                _registerState = Get<Action<string, Func<object[]>>>(t, "RegisterStateProvider");
                _unregisterState = Get<Action<string>>(t, "UnregisterStateProvider");
                _mark = Get<Action<string>>(t, "Mark");
                _registerLever = Get<Action<string, Action, Action>>(t, "RegisterAblationLever");

                if (_isEnabled == null) return;   // partial table - try again next call
                _bound = true;

                // flush any registrations made before the host was up
                for (int i = 0; i < _pending.Count; i++) { try { _pending[i](); } catch { } }
                _pending.Clear();
            }
            catch { /* any failure -> stays a no-op, retries next call */ }
        }

        private static T Get<T>(Type t, string field) where T : class
        {
            object v = t.GetField(field, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return v as T;   // works because Func<>/Action<> are shared BCL types in both assemblies
        }

        private static Type FindBridge(bool scan)
        {
            Type t = Type.GetType("Snitch.Bridge.SnitchBridge, Snitch", false);
            if (t != null || !scan) return t;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType("Snitch.Bridge.SnitchBridge", false); if (t != null) return t; }
                catch { }
            }
            return null;
        }

        internal static void InvokeEndScope(int token) { _endScope?.Invoke(token); }
    }

    /// <summary>Zero-heap-alloc timing scope returned by <see cref="Profiler.Sample"/>.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly int _token;
        internal Scope(int token) { _token = token; }
        public void Dispose() { if (_token != 0) Profiler.InvokeEndScope(_token); }
    }

    /// <summary>A small ordered name-&gt;count distribution returned by a state provider.</summary>
    public sealed class StateSnapshot
    {
        public string Title;
        public int Total;
        public readonly List<Bucket> Buckets = new List<Bucket>();
        public StateSnapshot Add(string name, int count) { Buckets.Add(new Bucket(name, count)); return this; }
        public void Clear() { Total = 0; Buckets.Clear(); }
    }

    public readonly struct Bucket
    {
        public readonly string Name;
        public readonly int Count;
        public Bucket(string name, int count) { Name = name; Count = count; }
    }
}
