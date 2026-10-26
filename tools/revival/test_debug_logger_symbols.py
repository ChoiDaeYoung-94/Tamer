"""Compile the real logger and call sites with PowerShell 7's C# compiler.

Only the fixed public logger source and this synthetic UnityEngine.Debug sink
are compiled. No Unity Editor, private assets, services, or SDK are loaded.
Use --pwsh PATH when PowerShell 7 is not available on PATH.
"""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


LOGGER_SOURCE = (
    Path(__file__).resolve().parents[2]
    / "Assets/Scripts/Foundations/DebugLogger.cs"
)
PWSH = shutil.which("pwsh")

FIXTURE = r'''
namespace UnityEngine
{
    public static class Debug
    {
        public static readonly System.Collections.Generic.List<string> Entries =
            new System.Collections.Generic.List<string>();

        public static void Log(string message) { Entries.Add("Log|" + message); }
        public static void LogWarning(string message) { Entries.Add("Warning|" + message); }
        public static void LogError(string message) { Entries.Add("Error|" + message); }
    }
}

public static class LoggerSymbolProbe
{
    public static int SideEffects;

    private static string Evaluate(string value)
    {
        SideEffects++;
        return value;
    }

    public static void RunCalls()
    {
        SideEffects = 0;
        UnityEngine.Debug.Entries.Clear();
        AD.DebugLogger.Log(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogWarning(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogError(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogDataError(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogParseError(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogNotFound(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogLoadError(Evaluate("where"), Evaluate("message"));
        AD.DebugLogger.LogInstantiateError(Evaluate("where"), Evaluate("message"));
    }

    // Reflection bypasses the outer Conditional call-site decision, allowing
    // the six nested PrintLog calls to be tested in release as well.
    public static void RunBodies()
    {
        UnityEngine.Debug.Entries.Clear();
        string[] methods = {
            "Log", "LogWarning", "LogError", "LogDataError", "LogParseError",
            "LogNotFound", "LogLoadError", "LogInstantiateError"
        };
        foreach (string method in methods)
            typeof(AD.DebugLogger).GetMethod(method).Invoke(
                null, new object[] { "where", "message" });
    }

    public static string[] DefinedSymbols()
    {
        var symbols = new System.Collections.Generic.List<string>();
#if DEBUG
        symbols.Add("DEBUG");
#endif
#if Debug
        symbols.Add("Debug");
#endif
#if DEVELOPMENT_BUILD
        symbols.Add("DEVELOPMENT_BUILD");
#endif
#if UNITY_EDITOR
        symbols.Add("UNITY_EDITOR");
#endif
        return symbols.ToArray();
    }
}
'''

RUNNER = r'''
param([string]$LoggerPath, [string]$FixturePath, [string]$Symbols)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell 7 or newer is required for the C# behavior fixture.'
}
$compile = @{ Path = @($LoggerPath, $FixturePath); ErrorAction = 'Stop' }
if ($Symbols) { $compile.CompilerOptions = @('/define:' + $Symbols) }
Add-Type @compile
[LoggerSymbolProbe]::RunCalls()
$sideEffects = [LoggerSymbolProbe]::SideEffects
$calls = @([UnityEngine.Debug]::Entries.ToArray())
[LoggerSymbolProbe]::RunBodies()
@{
    symbols = @([LoggerSymbolProbe]::DefinedSymbols())
    calls = $calls
    sideEffects = $sideEffects
    forcedBodies = @([UnityEngine.Debug]::Entries.ToArray())
    powershell = $PSVersionTable.PSVersion.ToString()
    runtime = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
} | ConvertTo-Json -Compress -Depth 4
'''

EXPECTED_ENTRIES = [
    "Log|<color=cyan>Log</color> - where\nmessage",
    "Warning|<color=yellow>LogWarning</color> - where\n<color=cyan>message</color>",
    "Error|<color=red>LogError</color> - where\n<color=cyan>message</color>",
    "Log|<color=red>Data Error</color> - where\nFailed to get data: message",
    "Log|<color=red>Parse Error</color> - where\nFailed to parse: message",
    'Log|<color=red>Not Found</color> - where\nThere is no "message"',
    "Log|<color=red>Load Error</color> - where\nFailed to load - path: message",
    "Log|<color=red>Instantiate Error</color> - where\nFailed to instantiate - path: message",
]


def compile_and_run(symbols):
    """Use a fresh process so Add-Type cannot reuse a previous symbol variant."""
    if PWSH is None:
        raise unittest.SkipTest(
            "PowerShell 7 (pwsh) not found; C# compiler behavior was not tested. "
            "Run this file with --pwsh PATH."
        )
    # TemporaryDirectory contains only the two synthetic files owned here.
    with tempfile.TemporaryDirectory(prefix="tamer-debug-logger-symbols-") as directory:
        directory = Path(directory)
        fixture_path = directory / "LoggerSymbolProbe.cs"
        runner_path = directory / "Run-LoggerSymbolProbe.ps1"
        fixture_path.write_text(FIXTURE, encoding="utf-8")
        runner_path.write_text(RUNNER, encoding="utf-8")
        completed = subprocess.run(
            [
                PWSH, "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                str(runner_path), "-LoggerPath", str(LOGGER_SOURCE),
                "-FixturePath", str(fixture_path), "-Symbols", ",".join(symbols),
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=30,
            check=False,
        )
    if completed.returncode:
        raise AssertionError(
            f"C# compile/run failed (exit {completed.returncode}):\n"
            f"{completed.stdout}\n{completed.stderr}"
        )
    return json.loads(completed.stdout)


class DebugLoggerSymbolTests(unittest.TestCase):
    def check_symbols(self, symbols, enabled):
        result = compile_and_run(symbols)
        source_hash = hashlib.sha256(LOGGER_SOURCE.read_bytes()).hexdigest()
        print(
            f"symbols={','.join(symbols) or '<none>'} "
            f"calls={len(result['calls'])} sideEffects={result['sideEffects']} "
            f"forcedBodies={len(result['forcedBodies'])} "
            f"PowerShell={result['powershell']} runtime={result['runtime']} "
            f"sourceSha256={source_hash}",
            flush=True,
        )
        self.assertEqual(sorted(symbols), sorted(result["symbols"]))
        self.assertEqual(EXPECTED_ENTRIES if enabled else [], result["calls"])
        self.assertEqual(16 if enabled else 0, result["sideEffects"])
        # Warning/Error write directly; the other six go through PrintLog.
        self.assertEqual(
            EXPECTED_ENTRIES if enabled else EXPECTED_ENTRIES[1:3],
            result["forcedBodies"],
        )

    def test_release_omits_calls_and_argument_evaluation(self):
        self.check_symbols((), enabled=False)

    def test_debug_uppercase_alone_keeps_release_silent(self):
        self.check_symbols(("DEBUG",), enabled=False)

    def test_debug_mixed_case_alone_keeps_release_silent(self):
        self.check_symbols(("Debug",), enabled=False)

    def test_development_build_keeps_calls(self):
        self.check_symbols(("DEVELOPMENT_BUILD",), enabled=True)

    def test_unity_editor_keeps_calls(self):
        self.check_symbols(("UNITY_EDITOR",), enabled=True)

    def test_editor_and_development_are_or_without_duplicate_calls(self):
        self.check_symbols(("UNITY_EDITOR", "DEVELOPMENT_BUILD"), enabled=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--pwsh", type=Path)
    options, remaining = parser.parse_known_args()
    if options.pwsh is not None:
        PWSH = str(options.pwsh.resolve())
    unittest.main(argv=[__file__, *remaining])
