using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;

namespace stationeers.modding.exporter
{

    [InitializeOnLoad]
    public static class CompileMonitor
    {
        // Public snapshot of the last pass
        public static bool LastPassHadErrors { get; private set; }
        public static IReadOnlyList<string> LastPassErrorAssemblies => _lastErrorAssemblies;

        // Internal state for the current pass
        static bool _passActive;
        static bool _passHadErrors;
        static readonly HashSet<string> _errorAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> _lastErrorAssemblies = new List<string>();
        static TaskCompletionSource<bool> _currentTcs; // completes when *this* pass ends

        static CompileMonitor()
        {
            // Listen for every compile as soon as the domain loads.
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // Reset state when the editor enters compiling; this covers cases where
            // the first assembly callback comes a bit later.
            EditorApplication.update += DetectCompileStart;
        }

        static bool _wasCompiling;
        static void DetectCompileStart()
        {
            if (EditorApplication.isCompiling && !_wasCompiling)
            {
                // A new compilation just started
                _wasCompiling = true;
                _passActive = true;
                _passHadErrors = false;
                _errorAssemblies.Clear();
                _currentTcs = new TaskCompletionSource<bool>();
            }
            else if (!EditorApplication.isCompiling && _wasCompiling)
            {
                _wasCompiling = false;
                // completion will be handled by OnCompilationFinished
            }
        }

        static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!_passActive)
            {
                // Be resilient if we didn’t see the start yet
                _passActive = true;
                _passHadErrors = false;
                _errorAssemblies.Clear();
                _currentTcs ??= new TaskCompletionSource<bool>();
            }

            if (messages.Any(m => m.type == CompilerMessageType.Error))
            {
                _passHadErrors = true;
                _errorAssemblies.Add(assemblyPath);
            }
        }

        static void OnCompilationFinished(object _)
        {
            _passActive = false;
            LastPassHadErrors = _passHadErrors;

            _lastErrorAssemblies.Clear();
            _lastErrorAssemblies.AddRange(_errorAssemblies);

            _currentTcs?.TrySetResult(true);
            _currentTcs = null;
        }

        /// Triggers a refresh (optional) and returns when the *current/next* compile pass ends.
        public static async Task<bool> WaitForCompilePassAsync(bool triggerRefresh)
        {
            // If no compile is active yet, prepare a TCS for the next one.
            if (!EditorApplication.isCompiling)
            {
                _passActive = true;
                _passHadErrors = false;
                _errorAssemblies.Clear();
                _currentTcs = new TaskCompletionSource<bool>();
            }

            if (triggerRefresh)
                AssetDatabase.Refresh(); // kicks off a compile if needed

            // If we somehow finished instantly, bail out.
            if (!EditorApplication.isCompiling && _currentTcs == null)
                return LastPassHadErrors;

            // Wait for the pass to finish.
            await (_currentTcs?.Task ?? Task.CompletedTask);
            return LastPassHadErrors;
        }
    }
}
