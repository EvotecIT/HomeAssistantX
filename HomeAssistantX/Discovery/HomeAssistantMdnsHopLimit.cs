using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace HomeAssistantX.Discovery;

internal static class HomeAssistantMdnsHopLimit
{
    private const int IpProtocol = 0;
    private const int WindowsReceiveTtl = 21;
    private const int LinuxReceiveTtl = 12;
    private const int DarwinReceiveTtl = 24;
    private const int WindowsTtlControl = 4;
    private const int LinuxTtlControl = 2;
    private const int DarwinTtlControl = 24;
    private const int MessagePeek = 0x2;
    private const int ControlBufferBytes = 256;
    private const int SocketErrorResult = -1;
    private const int SioGetExtensionFunctionPointer = unchecked((int)0xC8000006);
    private static readonly Guid WsaReceiveMessageId = new("f689d7c8-6f1f-436b-8a53-e54fe351c322");

    internal static void Configure(Socket socket)
    {
        if (socket is null) throw new ArgumentNullException(nameof(socket));
        var enabled = 1;
        var error = WithSocketHandle(socket, handle => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsSetSocketOption(handle, IpProtocol, WindowsReceiveTtl, ref enabled, sizeof(int))
            : UnixSetSocketOption(
                handle,
                IpProtocol,
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? DarwinReceiveTtl : LinuxReceiveTtl,
                ref enabled,
                sizeof(int)));
        if (error != 0) throw new SocketException(GetLastSocketError());
    }

    internal static int Peek(Socket socket)
    {
        if (socket is null) throw new ArgumentNullException(nameof(socket));
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? PeekWindows(socket)
            : PeekUnix(socket);
    }

    internal static bool IsExpected(int timeToLive) => timeToLive == 255;

    private static int PeekWindows(Socket socket)
    {
        var data = Marshal.AllocHGlobal(1);
        var buffers = Marshal.AllocHGlobal(Marshal.SizeOf<WindowsBuffer>());
        var control = Marshal.AllocHGlobal(ControlBufferBytes);
        try
        {
            Marshal.WriteByte(data, 0);
            Marshal.StructureToPtr(
                new WindowsBuffer { Length = 1, Buffer = data },
                buffers,
                fDeleteOld: false);
            var message = new WindowsMessage
            {
                Buffers = buffers,
                BufferCount = 1,
                Control = new WindowsBuffer { Length = ControlBufferBytes, Buffer = control },
                Flags = MessagePeek
            };
            var result = WithSocketHandle(socket, handle =>
            {
                var receiver = GetWindowsReceiveMessage(handle);
                return receiver(handle, ref message, out _, IntPtr.Zero, IntPtr.Zero);
            });
            if (result == SocketErrorResult) throw new SocketException(GetLastSocketError());
            return FindTimeToLive(control, checked((int)message.Control.Length), WindowsTtlControl);
        }
        finally
        {
            Marshal.FreeHGlobal(control);
            Marshal.FreeHGlobal(buffers);
            Marshal.FreeHGlobal(data);
        }
    }

    private static int PeekUnix(Socket socket)
    {
        var data = Marshal.AllocHGlobal(1);
        var vectors = Marshal.AllocHGlobal(Marshal.SizeOf<UnixIoVector>());
        var control = Marshal.AllocHGlobal(ControlBufferBytes);
        try
        {
            Marshal.WriteByte(data, 0);
            Marshal.StructureToPtr(
                new UnixIoVector { Base = data, Length = (UIntPtr)1u },
                vectors,
                fDeleteOld: false);
            long received;
            var controlLength = ControlBufferBytes;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var message = new DarwinMessage
                {
                    Vectors = vectors,
                    VectorCount = 1,
                    Control = control,
                    ControlLength = ControlBufferBytes
                };
                received = WithSocketHandle(socket, handle =>
                    DarwinReceiveMessage(handle, ref message, MessagePeek).ToInt64());
                controlLength = checked((int)message.ControlLength);
            }
            else
            {
                var message = new LinuxMessage
                {
                    Vectors = vectors,
                    VectorCount = (UIntPtr)1u,
                    Control = control,
                    ControlLength = (UIntPtr)ControlBufferBytes
                };
                received = WithSocketHandle(socket, handle =>
                    LinuxReceiveMessage(handle, ref message, MessagePeek).ToInt64());
                controlLength = checked((int)message.ControlLength.ToUInt64());
            }

            if (received == SocketErrorResult) throw new SocketException(GetLastSocketError());
            var controlType = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? DarwinTtlControl
                : LinuxTtlControl;
            return FindTimeToLive(control, controlLength, controlType);
        }
        finally
        {
            Marshal.FreeHGlobal(control);
            Marshal.FreeHGlobal(vectors);
            Marshal.FreeHGlobal(data);
        }
    }

    private static int FindTimeToLive(
        IntPtr control,
        int controlLength,
        int expectedType)
    {
        var offset = 0;
        var alignment = IntPtr.Size;
        while (offset >= 0 && offset < controlLength)
        {
            var header = IntPtr.Add(control, offset);
            var messageLength = IntPtr.Size == 8
                ? checked((int)unchecked((ulong)Marshal.ReadInt64(header)))
                : Marshal.ReadInt32(header);
            var rawHeaderLength = IntPtr.Size + sizeof(int) + sizeof(int);
            var dataOffset = Align(rawHeaderLength, alignment);
            if (messageLength < dataOffset || messageLength > controlLength - offset) break;
            var level = Marshal.ReadInt32(header, IntPtr.Size);
            var type = Marshal.ReadInt32(header, IntPtr.Size + sizeof(int));
            if (level == IpProtocol && type == expectedType)
            {
                var valueLength = messageLength - dataOffset;
                if (valueLength >= sizeof(int)) return Marshal.ReadInt32(header, dataOffset) & 0xFF;
                if (valueLength >= 1) return Marshal.ReadByte(header, dataOffset);
                break;
            }

            var next = Align(messageLength, alignment);
            if (next <= 0 || next > controlLength - offset) break;
            offset += next;
        }

        throw new SocketException((int)SocketError.ProtocolOption);
    }

    private static int Align(int value, int alignment)
        => checked((value + alignment - 1) & ~(alignment - 1));

    private static WindowsReceiveMessage GetWindowsReceiveMessage(IntPtr socket)
    {
        var bytes = 0u;
        var receiveMessageId = WsaReceiveMessageId;
        var result = WindowsSocketIoControl(
            socket,
            SioGetExtensionFunctionPointer,
            ref receiveMessageId,
            checked((uint)Marshal.SizeOf<Guid>()),
            out var function,
            checked((uint)IntPtr.Size),
            out bytes,
            IntPtr.Zero,
            IntPtr.Zero);
        if (result == SocketErrorResult) throw new SocketException(GetLastSocketError());
        return Marshal.GetDelegateForFunctionPointer<WindowsReceiveMessage>(function);
    }

    private static T WithSocketHandle<T>(Socket socket, Func<IntPtr, T> action)
    {
#if NET10_0
        var addedRef = false;
        try
        {
            socket.SafeHandle.DangerousAddRef(ref addedRef);
            return action(socket.SafeHandle.DangerousGetHandle());
        }
        finally
        {
            if (addedRef) socket.SafeHandle.DangerousRelease();
        }
#else
#pragma warning disable CS0618
        var result = action(socket.Handle);
#pragma warning restore CS0618
        GC.KeepAlive(socket);
        return result;
#endif
    }

    private static int GetLastSocketError() => Marshal.GetLastWin32Error();

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsBuffer
    {
        internal uint Length;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsMessage
    {
        internal IntPtr Name;
        internal int NameLength;
        internal IntPtr Buffers;
        internal uint BufferCount;
        internal WindowsBuffer Control;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnixIoVector
    {
        internal IntPtr Base;
        internal UIntPtr Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxMessage
    {
        internal IntPtr Name;
        internal uint NameLength;
        internal IntPtr Vectors;
        internal UIntPtr VectorCount;
        internal IntPtr Control;
        internal UIntPtr ControlLength;
        internal int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DarwinMessage
    {
        internal IntPtr Name;
        internal uint NameLength;
        internal IntPtr Vectors;
        internal int VectorCount;
        internal IntPtr Control;
        internal uint ControlLength;
        internal int Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int WindowsReceiveMessage(
        IntPtr socket,
        ref WindowsMessage message,
        out uint bytesReceived,
        IntPtr overlapped,
        IntPtr completionRoutine);

    [DllImport("ws2_32.dll", EntryPoint = "setsockopt", SetLastError = true)]
    private static extern int WindowsSetSocketOption(
        IntPtr socket,
        int level,
        int option,
        ref int value,
        int valueLength);

    [DllImport("ws2_32.dll", EntryPoint = "WSAIoctl", SetLastError = true)]
    private static extern int WindowsSocketIoControl(
        IntPtr socket,
        int controlCode,
        ref Guid input,
        uint inputLength,
        out IntPtr output,
        uint outputLength,
        out uint bytesReturned,
        IntPtr overlapped,
        IntPtr completionRoutine);

    [DllImport("libc", EntryPoint = "setsockopt", SetLastError = true)]
    private static extern int UnixSetSocketOption(
        IntPtr socket,
        int level,
        int option,
        ref int value,
        int valueLength);

    [DllImport("libc", EntryPoint = "recvmsg", SetLastError = true)]
    private static extern IntPtr LinuxReceiveMessage(
        IntPtr socket,
        ref LinuxMessage message,
        int flags);

    [DllImport("libc", EntryPoint = "recvmsg", SetLastError = true)]
    private static extern IntPtr DarwinReceiveMessage(
        IntPtr socket,
        ref DarwinMessage message,
        int flags);
}
