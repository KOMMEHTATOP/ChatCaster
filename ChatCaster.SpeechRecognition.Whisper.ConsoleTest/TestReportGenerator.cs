
using Serilog;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

/// <summary>
/// Генератор компактных отчетов по тестам без debug-логов
/// </summary>
public class TestReportGenerator
{
    private readonly List<string> _reportLines = new();
    private readonly List<string> _warnings = new();
    private readonly List<string> _errors = new();

    public void AddReportLine(string line)
    {
        _reportLines.Add(line);
    }

    public void AddWarning(string warning)
    {
        _warnings.Add($"⚠️ {warning}");
    }

    public void AddError(string error)
    {
        _errors.Add($"❌ {error}");
    }

    public void AddMemoryTable(List<ModelSwitchResult> results)
    {
        if (results.Count == 0) return;

        _reportLines.Add("");
        _reportLines.Add("📊 MEMORY USAGE REPORT");
        _reportLines.Add("".PadRight(80, '='));
        _reportLines.Add("");
        
        // Working Set Memory Table
        _reportLines.Add("🔹 WORKING SET MEMORY (Real Process Memory):");
        _reportLines.Add("┌─────────────────────────┬────────┬─────────────┬──────────────────┬─────────────────────┬────────┐");
        _reportLines.Add("│ Action                  │ Model  │ Idle (MB)   │ Recognition (MB) │ After Operation (MB)│ Status │");
        _reportLines.Add("├─────────────────────────┼────────┼─────────────┼──────────────────┼─────────────────────┼────────┤");
        
        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            var action = result.StepName.Length > 23 ? result.StepName[..20] + "..." : result.StepName;
            
            if (result.Success)
            {
                _reportLines.Add($"│ {action,-23} │ {result.ModelSize,-6} │ {result.MemoryBeforeMB,11:F0} │ {result.MemoryAfterRecognitionMB,16:F0} │ {result.MemoryAfterGCMB,19:F0} │ {status,6} │");
            }
            else
            {
                _reportLines.Add($"│ {action,-23} │ {result.ModelSize,-6} │ {"CRASH",-11} │ {"CRASH",-16} │ {"CRASH",-19} │ {status,6} │");
            }
        }
        
        _reportLines.Add("└─────────────────────────┴────────┴─────────────┴──────────────────┴─────────────────────┴────────┘");

        // Memory Analysis
        var successfulResults = results.Where(r => r.Success).ToList();
        if (successfulResults.Count > 1)
        {
            var firstMemory = successfulResults.First().MemoryAfterGCMB;
            var lastMemory = successfulResults.Last().MemoryAfterGCMB;
            var memoryGrowth = lastMemory - firstMemory;
            var maxMemory = successfulResults.Max(r => r.MemoryAfterRecognitionMB);
            var avgMemory = successfulResults.Average(r => r.MemoryAfterGCMB);

            _reportLines.Add("");
            _reportLines.Add("📈 MEMORY ANALYSIS:");
            _reportLines.Add($"• Initial memory: {firstMemory:F0} MB");
            _reportLines.Add($"• Final memory: {lastMemory:F0} MB");
            _reportLines.Add($"• Memory growth: {memoryGrowth:F0} MB ({(memoryGrowth/firstMemory*100):F1}%)");
            _reportLines.Add($"• Peak memory: {maxMemory:F0} MB");
            _reportLines.Add($"• Average memory: {avgMemory:F0} MB");

            if (memoryGrowth > 50)
            {
                AddWarning($"Significant memory growth: {memoryGrowth:F0} MB");
            }
            else if (memoryGrowth < 5)
            {
                _reportLines.Add("✅ Memory usage is stable - no significant leaks");
            }
        }

        // Model Size Analysis
        _reportLines.Add("");
        _reportLines.Add("📏 MODEL SIZE IMPACT:");
        var modelGroups = successfulResults.GroupBy(r => r.ModelSize).ToList();
        foreach (var group in modelGroups.OrderBy(g => g.Average(r => r.MemoryAfterGCMB)))
        {
            var avgMemory = group.Average(r => r.MemoryAfterGCMB);
            var maxMemory = group.Max(r => r.MemoryAfterRecognitionMB);
            _reportLines.Add($"• {group.Key,-6}: Avg {avgMemory:F0} MB, Peak {maxMemory:F0} MB ({group.Count()} switches)");
        }
    }

    public void AddPerformanceTable(List<ModelSwitchResult> results)
    {
        if (results.Count == 0) return;

        _reportLines.Add("");
        _reportLines.Add("⚡ PERFORMANCE REPORT");
        _reportLines.Add("".PadRight(80, '='));
        _reportLines.Add("");

        var successfulResults = results.Where(r => r.Success).ToList();
        
        // Performance by model
        var modelGroups = successfulResults.GroupBy(r => r.ModelSize).ToList();
        _reportLines.Add("🔹 AVERAGE PERFORMANCE BY MODEL:");
        _reportLines.Add("┌────────┬─────────────┬──────────────────┬─────────────────┐");
        _reportLines.Add("│ Model  │ Init (ms)   │ Recognition (ms) │ Total (ms)      │");
        _reportLines.Add("├────────┼─────────────┼──────────────────┼─────────────────┤");
        
        foreach (var group in modelGroups.OrderBy(g => g.Key))
        {
            var avgInit = group.Average(r => r.InitTimeMs);
            var avgRecognition = group.Average(r => r.RecognitionTimeMs);
            var avgTotal = avgInit + avgRecognition;
            
            _reportLines.Add($"│ {group.Key,-6} │ {avgInit,11:F0} │ {avgRecognition,16:F0} │ {avgTotal,15:F0} │");
        }
        
        _reportLines.Add("└────────┴─────────────┴──────────────────┴─────────────────┘");

        // Performance insights
        _reportLines.Add("");
        _reportLines.Add("📊 PERFORMANCE INSIGHTS:");
        
        var fastestModel = modelGroups.OrderBy(g => g.Average(r => r.RecognitionTimeMs)).First();
        var slowestModel = modelGroups.OrderByDescending(g => g.Average(r => r.RecognitionTimeMs)).First();
        
        _reportLines.Add($"• Fastest model: {fastestModel.Key} ({fastestModel.Average(r => r.RecognitionTimeMs):F0} ms avg)");
        _reportLines.Add($"• Slowest model: {slowestModel.Key} ({slowestModel.Average(r => r.RecognitionTimeMs):F0} ms avg)");
        
        var slowInitializations = successfulResults.Where(r => r.InitTimeMs > 1000).ToList();
        if (slowInitializations.Any())
        {
            AddWarning($"Slow initializations detected: {slowInitializations.Count} cases > 1 second");
        }
    }

    public void AddCrashAnalysis(List<ModelSwitchResult> results)
    {
        var crashes = results.Where(r => !r.Success).ToList();
        if (crashes.Count == 0) return;

        _reportLines.Add("");
        _reportLines.Add("💥 CRASH ANALYSIS");
        _reportLines.Add("".PadRight(80, '='));
        _reportLines.Add("");

        foreach (var crash in crashes)
        {
            AddError($"CRASH in '{crash.StepName}' (Model: {crash.ModelSize}): {crash.ErrorMessage}");
        }

        // Pattern analysis
        var crashModels = crashes.GroupBy(c => c.ModelSize).ToList();
        if (crashModels.Any())
        {
            _reportLines.Add("🔍 CRASH PATTERNS:");
            foreach (var group in crashModels)
            {
                _reportLines.Add($"• Model '{group.Key}': {group.Count()} crashes");
            }
        }
    }

    public void GenerateReport()
    {
        Console.Clear(); // Очищаем экран для чистого отчета
        
        // Header
        Log.Information("".PadRight(80, '='));
        Log.Information("🎯 WHISPER MODULE TEST REPORT");
        Log.Information("".PadRight(80, '='));
        Log.Information($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // Errors first (most important)
        if (_errors.Any())
        {
            Log.Information("🚨 ERRORS:");
            foreach (var error in _errors)
            {
                Log.Information($"   {error}");
            }
        }

        // Then warnings
        if (_warnings.Any())
        {
            Log.Information("⚠️ WARNINGS:");
            foreach (var warning in _warnings)
            {
                Log.Information($"   {warning}");
            }
        }

        // Main report
        foreach (var line in _reportLines)
        {
            Log.Information(line);
        }

        // Footer
        Log.Information("".PadRight(80, '='));
        if (_errors.Count == 0)
        {
            Log.Information("✅ ALL TESTS COMPLETED SUCCESSFULLY");
        }
        else
        {
            Log.Information($"❌ TESTS COMPLETED WITH {_errors.Count} ERRORS");
        }
        Log.Information("".PadRight(80, '='));
    }

    public void SaveToFile(string fileName = "whisper-test-report.txt")
    {
        var lines = new List<string>
        {
            "".PadRight(80, '='),
            "🎯 WHISPER MODULE TEST REPORT",
            "".PadRight(80, '='),
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ""
        };

        if (_errors.Any())
        {
            lines.Add("🚨 ERRORS:");
            lines.AddRange(_errors.Select(e => $"   {e}"));
            lines.Add("");
        }

        if (_warnings.Any())
        {
            lines.Add("⚠️ WARNINGS:");
            lines.AddRange(_warnings.Select(w => $"   {w}"));
            lines.Add("");
        }

        lines.AddRange(_reportLines);
        
        lines.Add("");
        lines.Add("".PadRight(80, '='));
        lines.Add(_errors.Count == 0 ? "✅ ALL TESTS COMPLETED SUCCESSFULLY" : $"❌ TESTS COMPLETED WITH {_errors.Count} ERRORS");
        lines.Add("".PadRight(80, '='));

        File.WriteAllLines(fileName, lines);
        Log.Information($"\n💾 Report saved to: {fileName}");
    }
    
    public void AddDiagnosticLog(string category, string message)
    {
        _reportLines.Add($"🔍 [{category}] {message}");
    }

    public void StartDiagnosticSection(string sectionName)
    {
        _reportLines.Add("");
        _reportLines.Add($"🔍 DIAGNOSTIC: {sectionName}");
        _reportLines.Add("".PadRight(50, '-'));
    }

}