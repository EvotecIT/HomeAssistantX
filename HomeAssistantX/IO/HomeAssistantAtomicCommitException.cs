namespace HomeAssistantX.IO;

internal sealed class HomeAssistantAtomicCommitException : IOException
{
    internal HomeAssistantAtomicCommitException(
        string message,
        Exception innerException,
        bool preserveTemporaryFile)
        : base(message, innerException)
    {
        PreserveTemporaryFile = preserveTemporaryFile;
    }

    internal bool PreserveTemporaryFile { get; }
}
