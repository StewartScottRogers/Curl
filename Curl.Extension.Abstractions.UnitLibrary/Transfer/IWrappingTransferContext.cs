namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// A transfer context a hook made around another, forwarding what it does not change. A context that wraps one carrying a
/// <see cref="RequestHeadersSigner" /> implements this, so that <see cref="TransferContextRequestSigning.SignedHeadersFor" />
/// finds the signer through it.
/// </summary>
public interface IWrappingTransferContext : ITransferContext
{
    /// <summary>Gets the context this one wraps.</summary>
    ITransferContext Inner { get; }
}
