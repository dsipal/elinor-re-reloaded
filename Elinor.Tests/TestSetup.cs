using System.Runtime.CompilerServices;
using Elinor;

namespace Elinor.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void RedirectLog()
    {
        Log.LogsDir = Path.Combine(Path.GetTempPath(), "elinor-test-logs");
    }
}
