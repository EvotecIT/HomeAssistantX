using System.Reflection;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static class HomeAssistantAtomicFile
{
    internal static void PreserveDestinationPermissions(string destinationPath, string temporaryPath)
        => PreserveDestinationPermissions(destinationPath, temporaryPath, useManagedApis: true);

    internal static void PreserveDestinationPermissions(
        string destinationPath,
        string temporaryPath,
        bool useManagedApis)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(destinationPath)) return;

        if (useManagedApis && TryPreserveWithManagedApis(destinationPath, temporaryPath)) return;
        PreserveWithNativeApis(destinationPath, temporaryPath);
    }

    private static bool TryPreserveWithManagedApis(string destinationPath, string temporaryPath)
    {
        var getMode = typeof(File).GetMethod(
            "GetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (getMode is null) return false;

        var mode = getMode.Invoke(null, new object[] { destinationPath });
        if (mode is null) return false;
        var setMode = typeof(File).GetMethod(
            "SetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), mode.GetType() },
            modifiers: null);
        if (setMode is null) return false;
        setMode.Invoke(null, new[] { (object)temporaryPath, mode });
        return true;
    }

    private static void PreserveWithNativeApis(string destinationPath, string temporaryPath)
    {
        var modeOffset = UnixModeOffset();
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (var offset = 0; offset < 512; offset += sizeof(long))
            {
                Marshal.WriteInt64(buffer, offset, 0L);
            }

            if (Stat(destinationPath, buffer) != 0)
            {
                throw new IOException(
                    "The destination Unix file mode could not be read.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            }

            const int permissionBits = 0x0FFF;
            var mode = unchecked((uint)(Marshal.ReadInt32(buffer, modeOffset) & permissionBits));
            if (Chmod(temporaryPath, mode) != 0)
            {
                throw new IOException(
                    "The temporary Unix file mode could not be updated.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int UnixModeOffset()
    {
        var operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "OSX"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : string.Empty;
        return UnixModeOffset(operatingSystem, RuntimeInformation.ProcessArchitecture.ToString());
    }

    internal static int UnixModeOffset(string operatingSystem, string architecture)
    {
        if (string.Equals(operatingSystem, "OSX", StringComparison.Ordinal)) return 4;
        if (!string.Equals(operatingSystem, "Linux", StringComparison.Ordinal))
            throw new PlatformNotSupportedException("Unix file-mode preservation supports Linux and macOS.");

        return architecture switch
        {
            "X64" or "S390x" or "Ppc64le" => 24,
            "X86" or "Arm" or "Arm64" or "Armv6" or "LoongArch64" or "RiscV64" => 16,
            _ => throw new PlatformNotSupportedException(
                "Unix file-mode preservation does not recognize the current Linux architecture.")
        };
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat(string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);
}
