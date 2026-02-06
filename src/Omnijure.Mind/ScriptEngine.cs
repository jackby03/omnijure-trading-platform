
using clojure.lang;
using System.IO;

namespace Omnijure.Mind;

public class ScriptEngine
{
    private readonly string _strategyPath;
    private FileSystemWatcher? _watcher;
    private Var? _tickFunction;
    public string LastDecision { get; private set; } = "WAITING";
    
    public ScriptEngine(string strategyPath)
    {
        _strategyPath = strategyPath;
        Initialize();
        SetupHotReload();
    }

    private void Initialize()
    {
        try 
        {
            // Boot Clojure Runtime
            // RT.load("clojure/core"); // implicitly loaded often
            
            // Load Strategy
            LoadStrategy();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mind] Init Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"[Mind] Inner: {ex.InnerException.Message}");
        }
    }

    private void LoadStrategy()
    {
        try
        {
            Console.WriteLine("[Mind] Reloading Strategy...");
            // Load the file. RT.loadResourceScript matches namespace to file path, but loadFile is direct.
            // clojure.lang.RT.load requires relative path in classpath or special handling.
            // Easiest way: Read text and Eval.
            
            string file = Path.Combine(_strategyPath, "core.clj");
            if (File.Exists(file))
            {
                string code = File.ReadAllText(file);
                // Use clojure.core/load-string to evaluate the file content
                RT.var("clojure.core", "load-string").invoke(code);
                
                // Bind function
                _tickFunction = RT.var("strategies.core", "on-tick");
                Console.WriteLine("[Mind] Strategy Loaded Successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mind] Load Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"[Mind] Inner: {ex.InnerException.Message}");
        }
    }

    private void SetupHotReload()
    {
        _watcher = new FileSystemWatcher(_strategyPath, "*.clj");
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Changed += (s, e) => {
            // Debounce if needed, but for now direct load
            Thread.Sleep(50); // Small buffer for file write lock
            LoadStrategy();
        };
        _watcher.EnableRaisingEvents = true;
    }

    // CHANGED: Now accepts a Map of signal values
    public void InvokeTick(System.Collections.IDictionary signals)
    {
        if (_tickFunction != null && _tickFunction.isBound)
        {
            try 
            {
                // ClojureCLR can interact with IDictionary usually, or we pass PersistentHashMap
                // Let's rely on interop magic or create a simple object array if needed.
                // Best way: RT.map(key1, val1, key2, val2...)
                
                // Constructing arguments for RT.map is painful from C#.
                // Let's pass the dictionary directly and let Clojure handle 'get'.
                
                object result = _tickFunction.invoke(signals);
                LastDecision = result?.ToString() ?? "NIL";
            }
            catch (Exception ex)
            {
                LastDecision = $"ERR: {ex.Message}";
            }
        }
    }
}
