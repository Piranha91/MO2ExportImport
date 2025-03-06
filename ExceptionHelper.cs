using System.Text;

namespace MO2ExportImport;

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

public static class ExceptionHelper
{
    public static string GetFilteredStackTrace(Exception ex)
    {
        string myNamespace = GetRootNamespace();

        var filteredStackTrace = string.Join("\n", new StackTrace(ex, true)
            .GetFrames()
            .Where(frame => frame.GetMethod()?.DeclaringType?.Namespace?.StartsWith(myNamespace) == true)
            .Select(frame =>
            {
                var method = frame.GetMethod();
                return $"{method?.DeclaringType}.{method?.Name} at {frame.GetFileName()}:{frame.GetFileLineNumber()}";
            }));

        return $"Exception: {ex.GetType().Name}\nMessage: {ex.Message}\n\nFiltered Stack Trace:\n{filteredStackTrace}";
    }

    private static string GetRootNamespace()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null) return string.Empty;

        var rootNamespace = entryAssembly.GetTypes()
            .Select(t => t.Namespace)
            .Where(ns => !string.IsNullOrEmpty(ns))
            .GroupBy(ns => ns)
            .OrderByDescending(g => g.Count()) // Most common namespace (your app's root)
            .Select(g => g.Key)
            .FirstOrDefault();

        return rootNamespace ?? string.Empty;
    }
    
    public static string GetFullExceptionMessage(Exception ex)
    {
        var sb = new StringBuilder();
        while (ex != null)
        {
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"StackTrace: {ex.StackTrace}");
            sb.AppendLine(new string('-', 50));
            ex = ex.InnerException;
        }
        return sb.ToString();
    }
}
