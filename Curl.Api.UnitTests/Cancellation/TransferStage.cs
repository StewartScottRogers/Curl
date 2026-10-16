namespace Curl.Api.Cancellation;

/// <summary>Where a <see cref="CancellableEasyTransfer"/> waits to be cancelled.</summary>
internal enum TransferStage
{
    /// <summary>No byte sent or received yet.</summary>
    Connecting,

    /// <summary>The read callback has supplied upload bytes.</summary>
    Uploading,

    /// <summary>The write callback has taken body bytes.</summary>
    Downloading,

    /// <summary>The write callback has answered pause and the transfer waits to be unpaused.</summary>
    Paused,
}
