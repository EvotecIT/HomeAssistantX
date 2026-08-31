namespace HomeAssistantX.IO;

internal sealed class HomeAssistantAtomicCommitException : IOException
{
    internal HomeAssistantAtomicCommitException(
        string message,
        Exception innerException,
        bool preserveTemporaryFile,
        string? recoveryPath = null)
        : base(message, innerException)
    {
        PreserveTemporaryFile = preserveTemporaryFile;
        RecoveryPath = recoveryPath;
    }

    internal bool PreserveTemporaryFile { get; }

    internal string? RecoveryPath { get; }
}
