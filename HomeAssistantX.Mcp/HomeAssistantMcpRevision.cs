using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HomeAssistantX.Mcp;

/// <summary>Computes the MCP editor's revision from the exact JSON returned by Home Assistant.</summary>
internal static class HomeAssistantMcpRevision
{
    internal static async Task<string> CreateAsync(JsonElement definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var computation = Task.Run(() => Compute(definition, cancellationToken), CancellationToken.None);
        try
        {
            return await computation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = computation.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
    }

    private static string Compute(JsonElement definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = definition.GetRawText();
        cancellationToken.ThrowIfCancellationRequested();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var encoder = Encoding.UTF8.GetEncoder();
        var buffer = ArrayPool<byte>.Shared.Rent(32_768);
        try
        {
            for (var offset = 0; offset < raw.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var length = Math.Min(8_192, raw.Length - offset);
                var count = encoder.GetBytes(
                    raw.AsSpan(offset, length),
                    buffer.AsSpan(),
                    flush: offset + length == raw.Length);
                hash.AppendData(buffer.AsSpan(0, count));
                offset += length;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
