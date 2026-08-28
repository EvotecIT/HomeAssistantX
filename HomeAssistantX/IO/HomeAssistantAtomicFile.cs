using System.Reflection;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static class HomeAssistantAtomicFile
{
    internal static void PreserveDestinationPermissions(string destinationPath, string temporaryPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(destinationPath)) return;

        var getMode = typeof(File).GetMethod(
            "GetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (getMode is null) return;

        var mode = getMode.Invoke(null, new object[] { destinationPath });
        if (mode is null) return;
        var setMode = typeof(File).GetMethod(
            "SetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), mode.GetType() },
            modifiers: null);
        setMode?.Invoke(null, new[] { (object)temporaryPath, mode });
    }
}
