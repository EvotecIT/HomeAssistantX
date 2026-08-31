using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    // Preserve ordinary permissions and the sticky bit, but never restore
    // setuid/setgid onto an inode containing newly exported bytes.
    private const int SafeReplacementModeBits = 0x03FF;
    private const int UnixFileTypeBits = 0xF000;
    private const int UnixRegularFile = 0x8000;
    private const int UnixSymbolicLink = 0xA000;
    private const string LinuxAccessAclAttribute = "system.posix_acl_access";
    private const string LinuxSecurityContextAttribute = "security.selinux";
    private const int UnixOperationNotPermitted = 1;
    private const int UnixNoEntry = 2;
    private const int UnixBadFileDescriptor = 9;
    private const int UnixPermissionDenied = 13;
    private const int LinuxNoData = 61;
    private const int LinuxOperationNotSupported = 95;
    private const int MacExtendedAcl = 0x00000100;
    private const int MacOperationNotSupported = 45;
    private const uint OwnerReadWriteMode = 0x180;
    private const int LinuxWriteOnly = 0x0001;
    private const int LinuxReadOnly = 0x0000;
    private const int LinuxCreate = 0x0040;
    private const int LinuxExclusive = 0x0080;
    private const int LinuxNoFollow = 0x20000;
    private const int LinuxCloseOnExec = 0x80000;
    private const int LinuxNonBlocking = 0x0800;
    private const int LinuxPathOnly = 0x200000;
    private const int MacWriteOnly = 0x0001;
    private const int MacReadOnly = 0x0000;
    private const int MacCreate = 0x0200;
    private const int MacExclusive = 0x0800;
    private const int MacNoFollow = 0x0100;
    private const int MacCloseOnExec = 0x1000000;
    private const int MacNonBlocking = 0x0004;
    private const int MacEventOnly = 0x8000;
    private const int MacOpenSymbolicLink = 0x200000;
    private const int UnixCurrentWorkingDirectory = -100;
    private const uint LinuxRenameExchange = 0x2;
    private const uint MacRenameSwap = 0x2;

    private static void CommitUnixOverwrite(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action? beforeMetadataRecheck,
        Action? afterExchange,
        Action? beforeCommit)
    {
        cancellationToken.ThrowIfCancellationRequested();
        beforeMetadataRecheck?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();

        using var replacementHandle = OpenPinnedUnixFile(temporaryPath);
        var replacementIdentity = ReadUnixFileMetadata(replacementHandle, includeAccessAcl: false);
        RequireRegularUnixFile(replacementIdentity, "The Unix replacement must be a regular file.");
        CommitUnixOverwriteCore(
            replacementHandle,
            replacementIdentity,
            temporaryPath,
            destinationPath,
            cancellationToken,
            afterExchange,
            beforeCommit);
    }

    private static void CommitUnixPinnedFile(
        SafeFileHandle replacementHandle,
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var replacementIdentity = ReadUnixFileMetadata(replacementHandle, includeAccessAcl: false);
        RequireRegularUnixFile(replacementIdentity, "The Unix replacement must be a regular file.");
        RequireUnixPathIdentity(temporaryPath, replacementIdentity);
        if (!overwrite)
        {
            MovePinnedUnixFile(
                temporaryPath,
                destinationPath,
                replacementIdentity,
                cancellationToken);
            return;
        }

        CommitUnixOverwriteCore(
            replacementHandle,
            replacementIdentity,
            temporaryPath,
            destinationPath,
            cancellationToken,
            afterExchange: null,
            beforeCommit: null);
    }

    private static void CommitUnixOverwriteCore(
        SafeFileHandle replacementHandle,
        UnixFileMetadata replacementIdentity,
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action? afterExchange,
        Action? beforeCommit)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireUnixPathIdentity(temporaryPath, replacementIdentity);

        if (TryReadUnixFileMetadata(destinationPath, includeAccessAcl: false) is null)
        {
            try
            {
                beforeCommit?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                MovePinnedUnixFile(
                    temporaryPath,
                    destinationPath,
                    replacementIdentity,
                    cancellationToken);
                return;
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                // A destination appeared after the absence check. Continue with
                // the pinned overwrite path instead of replacing it unchecked.
            }
        }

        using var displacedHandle = OpenPinnedUnixMetadataFile(destinationPath);
        var pinnedDisplaced = ReadUnixFileMetadata(displacedHandle, includeAccessAcl: false);
        if (!pinnedDisplaced.IsRegularFile && !pinnedDisplaced.IsSymbolicLink)
        {
            throw new IOException("The Unix destination must be a regular file or symbolic link.");
        }

        beforeCommit?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        RequireUnixPathIdentity(temporaryPath, replacementIdentity);
        var exchangeResult = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? RenameMac(temporaryPath, destinationPath, MacRenameSwap)
            : RenameLinux(
                UnixCurrentWorkingDirectory,
                temporaryPath,
                UnixCurrentWorkingDirectory,
                destinationPath,
                LinuxRenameExchange);
        if (exchangeResult == 0)
        {
            // The destination was pinned before the exchange. The displaced entry
            // must still name that exact inode before any of its metadata is applied.
            try
            {
                // Once the paths have been exchanged this is a non-cancellable
                // commit section: either finish or restore the displaced file.
                afterExchange?.Invoke();
                RequireUnixPathIdentity(temporaryPath, pinnedDisplaced);
                if (pinnedDisplaced.IsSymbolicLink)
                {
                    RequireUnixPathIdentity(temporaryPath, pinnedDisplaced);
                }
                else
                {
                    var displaced = ReadUnixFileMetadata(
                        displacedHandle,
                        includeAccessAcl: true);
                    RequireUnixPathIdentity(temporaryPath, pinnedDisplaced);
                    ApplyUnixDestinationMetadata(replacementHandle, displaced);
                    RequireUnixPathIdentity(temporaryPath, pinnedDisplaced);
                }

                RequireUnixPathIdentity(destinationPath, replacementIdentity);
                File.Delete(temporaryPath);
            }
            catch (Exception commitException)
            {
                if (!UnixPathMatchesIdentity(temporaryPath, pinnedDisplaced))
                {
                    throw new HomeAssistantAtomicCommitException(
                        "The Unix replacement could not be completed because the displaced original was moved by a concurrent directory change. "
                        + "The replacement remains at the destination with restrictive permissions; no recovery path can be verified.",
                        commitException,
                        preserveTemporaryFile: false);
                }

                var rollbackResult = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? RenameMac(destinationPath, temporaryPath, MacRenameSwap)
                    : RenameLinux(
                        UnixCurrentWorkingDirectory,
                        destinationPath,
                        UnixCurrentWorkingDirectory,
                        temporaryPath,
                        LinuxRenameExchange);
                if (rollbackResult == 0)
                {
                    throw new IOException(
                        "The Unix replacement could not be completed; the original destination was restored.",
                        commitException);
                }

                var rollbackError = new Win32Exception(Marshal.GetLastWin32Error());
                throw new HomeAssistantAtomicCommitException(
                    "The Unix replacement could not be completed or rolled back. "
                    + "The displaced original was preserved for recovery at '" + temporaryPath + "'.",
                    new AggregateException(commitException, rollbackError),
                    preserveTemporaryFile: true,
                    recoveryPath: temporaryPath);
            }
            return;
        }

        var exchangeError = Marshal.GetLastWin32Error();
        if (exchangeError == UnixNoEntry)
        {
            try
            {
                beforeCommit?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                MovePinnedUnixFile(
                    temporaryPath,
                    destinationPath,
                    replacementIdentity,
                    cancellationToken);
                return;
            }
            catch (IOException exception) when (File.Exists(destinationPath))
            {
                throw new IOException(
                    "The destination Unix file appeared while the atomic replacement was being committed; retry the export.",
                    exception);
            }
        }

        throw new IOException(
            "The Unix destination could not be exchanged atomically.",
            new Win32Exception(exchangeError));
    }

    private static void MovePinnedUnixFile(
        string temporaryPath,
        string destinationPath,
        UnixFileMetadata replacementIdentity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireUnixPathIdentity(temporaryPath, replacementIdentity);
        File.Move(temporaryPath, destinationPath);
        try
        {
            RequireUnixPathIdentity(destinationPath, replacementIdentity);
        }
        catch (Exception identityException)
        {
            Exception? rollbackException = null;
            try
            {
                if (File.Exists(temporaryPath))
                    throw new IOException("The Unix staging path was occupied during rollback.");
                File.Move(destinationPath, temporaryPath);
            }
            catch (Exception exception)
            {
                rollbackException = exception;
            }

            if (rollbackException is null)
            {
                throw new IOException(
                    "The Unix staging entry changed during commit; the unverified destination was removed.",
                    identityException);
            }

            throw new HomeAssistantAtomicCommitException(
                "The Unix staging entry changed during commit and the unverified destination could not be removed safely.",
                new AggregateException(identityException, rollbackException),
                preserveTemporaryFile: false);
        }
    }

    private static void PreserveUnixDestinationMetadata(
        string destinationPath,
        string temporaryPath,
        bool useManagedApis)
    {
        var sourceIdentity = TryReadUnixFileMetadata(destinationPath, includeAccessAcl: false);
        if (!sourceIdentity.HasValue || sourceIdentity.Value.IsSymbolicLink) return;
        RequireRegularUnixFile(
            sourceIdentity.Value,
            "The Unix destination must be a regular file or symbolic link.");

        using var sourceHandle = OpenPinnedUnixMetadataFile(destinationPath);
        var pinnedSource = ReadUnixFileMetadata(sourceHandle, includeAccessAcl: false);
        RequireRegularUnixFile(pinnedSource, "The pinned Unix destination must be a regular file.");
        if (!UnixFileMetadata.SameIdentity(sourceIdentity.Value, pinnedSource))
        {
            throw new IOException(
                "The Unix destination changed before its metadata could be pinned.");
        }
        var metadata = ReadUnixFileMetadata(
            sourceHandle,
            includeAccessAcl: true,
            allowProcDescriptorPath: useManagedApis);
        RequireUnixPathIdentity(destinationPath, pinnedSource);

        using var temporaryHandle = OpenPinnedUnixFile(temporaryPath);
        var temporaryIdentity = ReadUnixFileMetadata(temporaryHandle, includeAccessAcl: false);
        RequireRegularUnixFile(temporaryIdentity, "The Unix replacement must be a regular file.");
        ApplyUnixDestinationMetadata(temporaryHandle, metadata);
        RequireUnixPathIdentity(temporaryPath, temporaryIdentity);
    }

    private static void ApplyUnixDestinationMetadata(
        SafeFileHandle temporaryHandle,
        UnixFileMetadata metadata)
    {
        if (metadata.IsSymbolicLink) return;

        // Keep the publicly named staging inode inaccessible while ownership is
        // changing. Transfer ownership before applying the destination ACL so an
        // ACL update can never expose the exchanged inode under the exporter identity.
        // Applying an ACL can update POSIX mode bits, so expose the final mode last.
        SetUnixMode(temporaryHandle, 0);

        if (FChown(temporaryHandle, metadata.UserId, metadata.GroupId) != 0)
        {
            throw new IOException(
                "The temporary Unix file ownership could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ApplyLinuxAccessAcl(temporaryHandle, metadata.LinuxAccessAcl);
            ApplyLinuxSecurityContext(temporaryHandle, metadata.LinuxSecurityContext);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ApplyMacAccessAcl(
                temporaryHandle,
                metadata.MacAccessAcl
                    ?? throw new IOException("The snapshotted macOS access ACL was unavailable."));
        }

        SetUnixMode(temporaryHandle, 0);
        SetUnixMode(temporaryHandle, metadata.Mode);
    }

    private static void SetUnixMode(SafeFileHandle handle, uint mode)
    {
        if (FChmod(handle, mode) != 0)
        {
            throw new IOException(
                "The temporary Unix file mode could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static SafeFileHandle OpenPinnedUnixFile(string path)
    {
        var flags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacReadOnly | MacNoFollow | MacCloseOnExec | MacNonBlocking
            : LinuxReadOnly | LinuxNoFollow | LinuxCloseOnExec | LinuxNonBlocking;
        var descriptor = EnsureUsableUnixDescriptor(Open(path, flags, 0));
        if (descriptor < 0)
        {
            throw new IOException(
                "The Unix file could not be pinned without following symbolic links.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    private static SafeFileHandle OpenPinnedUnixMetadataFile(string path)
    {
        int descriptor;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            descriptor = EnsureUsableUnixDescriptor(
                Open(path, MacEventOnly | MacOpenSymbolicLink | MacNoFollow | MacCloseOnExec | MacNonBlocking, 0));
        }
        else
        {
            descriptor = EnsureUsableUnixDescriptor(
                Open(path, LinuxReadOnly | LinuxNoFollow | LinuxCloseOnExec | LinuxNonBlocking, 0));
            if (descriptor < 0)
            {
                descriptor = EnsureUsableUnixDescriptor(
                    Open(path, LinuxWriteOnly | LinuxNoFollow | LinuxCloseOnExec | LinuxNonBlocking, 0));
            }
            if (descriptor < 0)
            {
                descriptor = EnsureUsableUnixDescriptor(
                    Open(path, LinuxPathOnly | LinuxNoFollow | LinuxCloseOnExec, 0));
            }
        }
        if (descriptor < 0)
        {
            throw new IOException(
                "The Unix file metadata could not be pinned without requiring content access.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    private static FileStream CreateSecureUnixTemporaryFileStream(string temporaryPath)
    {
        var flags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacWriteOnly | MacCreate | MacExclusive | MacCloseOnExec
            : LinuxWriteOnly | LinuxCreate | LinuxExclusive | LinuxCloseOnExec;
        var descriptor = EnsureUsableUnixDescriptor(Open(temporaryPath, flags, OwnerReadWriteMode));
        if (descriptor < 0)
        {
            throw new IOException(
                "The secure temporary file could not be created.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        try
        {
            // A SafeFileHandle created by native open is a synchronous handle on
            // Unix. Keep its managed buffer effectively empty so the shared
            // bounded writer never accumulates a second, uncancellable payload
            // in a final flush.
            return new FileStream(handle, FileAccess.Write, 1, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static UnixFileMetadata? TryReadUnixFileMetadata(
        string path,
        bool includeAccessAcl = true)
    {
        try
        {
            return ReadUnixFileMetadata(path, includeAccessAcl);
        }
        catch (IOException exception) when (
            exception.InnerException is Win32Exception native
            && native.NativeErrorCode == UnixNoEntry)
        {
            return null;
        }
    }

    private static UnixFileMetadata ReadUnixFileMetadata(
        string path,
        bool includeAccessAcl = true)
    {
        var offsets = UnixMetadataOffsets();
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (var offset = 0; offset < 512; offset += sizeof(long))
            {
                Marshal.WriteInt64(buffer, offset, 0L);
            }

            if (LStat(path, buffer) != 0)
            {
                throw new IOException(
                    "The destination Unix file metadata could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var rawMode = Marshal.ReadInt32(buffer, offsets.Mode);
            var fileType = rawMode & UnixFileTypeBits;
            var isRegularFile = fileType == UnixRegularFile;
            var linuxAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                ? ReadLinuxAccessAcl(path)
                : null;
            var linuxSecurityContext = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                ? ReadLinuxSecurityContext(path)
                : null;
            var macAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && isRegularFile
                ? ReadMacAccessAcl(path)
                : null;
            return new UnixFileMetadata(
                ReadNativeUnsigned(buffer, offsets.Device, offsets.DeviceIs32Bit),
                ReadNativeUnsigned(buffer, offsets.Inode, offsets.InodeIs32Bit),
                unchecked((uint)(rawMode & SafeReplacementModeBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)),
                unchecked((uint)fileType),
                linuxAccessAcl,
                linuxSecurityContext,
                macAccessAcl);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static UnixFileMetadata ReadUnixFileMetadata(
        SafeFileHandle handle,
        bool includeAccessAcl,
        bool allowProcDescriptorPath = true)
    {
        var offsets = UnixMetadataOffsets();
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (var offset = 0; offset < 512; offset += sizeof(long))
            {
                Marshal.WriteInt64(buffer, offset, 0L);
            }

            if (FStat(handle, buffer) != 0)
            {
                throw new IOException(
                    "The pinned Unix file metadata could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var rawMode = Marshal.ReadInt32(buffer, offsets.Mode);
            var fileType = rawMode & UnixFileTypeBits;
            var isRegularFile = fileType == UnixRegularFile;
            var linuxAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                    ? ReadLinuxAccessAcl(handle, allowProcDescriptorPath)
                    : null;
            var linuxSecurityContext = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                    ? ReadLinuxSecurityContext(handle, allowProcDescriptorPath)
                    : null;
            var macAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && isRegularFile
                    ? ReadMacAccessAcl(handle)
                    : null;
            return new UnixFileMetadata(
                ReadNativeUnsigned(buffer, offsets.Device, offsets.DeviceIs32Bit),
                ReadNativeUnsigned(buffer, offsets.Inode, offsets.InodeIs32Bit),
                unchecked((uint)(rawMode & SafeReplacementModeBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)),
                unchecked((uint)fileType),
                linuxAccessAcl,
                linuxSecurityContext,
                macAccessAcl);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RequireUnixPathIdentity(
        string path,
        UnixFileMetadata expected)
    {
        var current = ReadUnixFileMetadata(path, includeAccessAcl: false);
        if (!UnixFileMetadata.SameIdentity(expected, current))
        {
            throw new IOException(
                "The Unix file path no longer names the pinned inode.");
        }
    }

    private static bool UnixPathMatchesIdentity(
        string path,
        UnixFileMetadata expected)
    {
        try
        {
            return UnixFileMetadata.SameIdentity(
                expected,
                ReadUnixFileMetadata(path, includeAccessAcl: false));
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void RequireRegularUnixFile(UnixFileMetadata metadata, string message)
    {
        if (!metadata.IsRegularFile) throw new IOException(message);
    }

    private static int EnsureUsableUnixDescriptor(int descriptor)
        => EnsureUsableUnixDescriptor(descriptor, DuplicateDescriptor, CloseDescriptor);

    internal static int EnsureUsableUnixDescriptor(
        int descriptor,
        Func<int, int> duplicate,
        Func<int, int> close)
    {
        if (descriptor != 0) return descriptor;
        var duplicated = duplicate(descriptor);
        if (duplicated < 0)
        {
            var error = Marshal.GetLastWin32Error();
            _ = close(descriptor);
            throw new IOException(
                "Unix descriptor zero could not be duplicated into a safe handle.",
                new Win32Exception(error));
        }

        if (close(descriptor) == 0) return duplicated;
        var closeError = Marshal.GetLastWin32Error();
        _ = close(duplicated);
        throw new IOException(
            "Unix descriptor zero could not be closed after duplication.",
            new Win32Exception(closeError));
    }

    private static ulong ReadNativeUnsigned(IntPtr buffer, int offset, bool is32Bit)
        => is32Bit
            ? unchecked((uint)Marshal.ReadInt32(buffer, offset))
            : unchecked((ulong)Marshal.ReadInt64(buffer, offset));

    private static UnixMetadataOffset UnixMetadataOffsets()
    {
        var operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "OSX"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : string.Empty;
        return UnixMetadataOffsets(operatingSystem, RuntimeInformation.ProcessArchitecture.ToString());
    }

    internal static UnixMetadataOffset UnixMetadataOffsets(string operatingSystem, string architecture)
    {
        if (string.Equals(operatingSystem, "OSX", StringComparison.Ordinal))
            return new UnixMetadataOffset(0, 8, 4, 16, 20, deviceIs32Bit: true, inodeIs32Bit: false);
        if (!string.Equals(operatingSystem, "Linux", StringComparison.Ordinal))
            throw new PlatformNotSupportedException("Unix metadata preservation supports Linux and macOS.");

        return architecture switch
        {
            "X64" or "S390x" or "Ppc64le" =>
                new UnixMetadataOffset(0, 8, 24, 28, 32, deviceIs32Bit: false, inodeIs32Bit: false),
            "X86" or "Arm" or "Armv6" =>
                new UnixMetadataOffset(0, 12, 16, 24, 28, deviceIs32Bit: false, inodeIs32Bit: true),
            "Arm64" or "LoongArch64" or "RiscV64" =>
                new UnixMetadataOffset(0, 8, 16, 24, 28, deviceIs32Bit: false, inodeIs32Bit: false),
            _ => throw new PlatformNotSupportedException(
                "Unix metadata preservation does not recognize the current Linux architecture.")
        };
    }

    internal readonly struct UnixMetadataOffset
    {
        internal UnixMetadataOffset(
            int device,
            int inode,
            int mode,
            int userId,
            int groupId,
            bool deviceIs32Bit,
            bool inodeIs32Bit)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            DeviceIs32Bit = deviceIs32Bit;
            InodeIs32Bit = inodeIs32Bit;
        }

        internal int Device { get; }
        internal int Inode { get; }
        internal int Mode { get; }
        internal int UserId { get; }
        internal int GroupId { get; }
        internal bool DeviceIs32Bit { get; }
        internal bool InodeIs32Bit { get; }
    }

    private readonly struct UnixFileMetadata
    {
        internal UnixFileMetadata(
            ulong device,
            ulong inode,
            uint mode,
            uint userId,
            uint groupId,
            uint fileType,
            byte[]? linuxAccessAcl,
            byte[]? linuxSecurityContext,
            string? macAccessAcl)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            FileType = fileType;
            LinuxAccessAcl = linuxAccessAcl;
            LinuxSecurityContext = linuxSecurityContext;
            MacAccessAcl = macAccessAcl;
        }

        internal ulong Device { get; }
        internal ulong Inode { get; }
        internal uint Mode { get; }
        internal uint UserId { get; }
        internal uint GroupId { get; }
        internal uint FileType { get; }
        internal bool IsRegularFile => FileType == UnixRegularFile;
        internal bool IsSymbolicLink => FileType == UnixSymbolicLink;
        internal byte[]? LinuxAccessAcl { get; }
        internal byte[]? LinuxSecurityContext { get; }
        internal string? MacAccessAcl { get; }

        internal static bool SameIdentity(
            UnixFileMetadata left,
            UnixFileMetadata right)
            => left.Device == right.Device
                && left.Inode == right.Inode
                && left.FileType == right.FileType;

        internal static bool SameIdentityAndPermissions(
            UnixFileMetadata? expected,
            UnixFileMetadata? current)
        {
            if (!expected.HasValue || !current.HasValue)
            {
                return expected.HasValue == current.HasValue;
            }

            return expected.Value.Device == current.Value.Device
                && expected.Value.Inode == current.Value.Inode
                && expected.Value.Mode == current.Value.Mode
                && expected.Value.UserId == current.Value.UserId
                && expected.Value.GroupId == current.Value.GroupId
                && expected.Value.FileType == current.Value.FileType
                && EqualBytes(expected.Value.LinuxAccessAcl, current.Value.LinuxAccessAcl)
                && EqualBytes(expected.Value.LinuxSecurityContext, current.Value.LinuxSecurityContext)
                && string.Equals(expected.Value.MacAccessAcl, current.Value.MacAccessAcl, StringComparison.Ordinal);
        }

        private static bool EqualBytes(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int DuplicateDescriptor(int descriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDescriptor(int descriptor);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStat(string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStat(SafeFileHandle handle, IntPtr buffer);

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int FChmod(SafeFileHandle handle, uint mode);

    [DllImport("libc", EntryPoint = "fchown", SetLastError = true)]
    private static extern int FChown(SafeFileHandle handle, uint owner, uint group);

    private static int RenameLinux(
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        uint flags)
        => RenameLinuxSystemCall(
            new IntPtr(GetLinuxRenameAt2SystemCallNumber(RuntimeInformation.ProcessArchitecture)),
            oldDirectory,
            oldPath,
            newDirectory,
            newPath,
            flags).ToInt32();

    internal static int GetLinuxRenameAt2SystemCallNumber(Architecture architecture)
        => architecture switch
        {
            Architecture.X64 => 316,
            Architecture.X86 => 353,
            Architecture.Arm => 382,
            Architecture.Arm64 => 276,
            _ => architecture.ToString() switch
            {
                "Armv6" => 382,
                "S390x" => 347,
                "Ppc64le" => 357,
                "LoongArch64" => 276,
                "RiscV64" => 276,
                _ => throw new PlatformNotSupportedException(
                    "Atomic Linux file replacement is not supported on this processor architecture.")
            }
        };

    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern IntPtr RenameLinuxSystemCall(
        IntPtr number,
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        uint flags);

    [DllImport("libc", EntryPoint = "renamex_np", SetLastError = true)]
    private static extern int RenameMac(string oldPath, string newPath, uint flags);

}
