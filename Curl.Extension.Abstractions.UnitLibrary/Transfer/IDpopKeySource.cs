namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// A transfer hook that signs the transfer's requests with a DPoP key (RFC 9449) and lends that key to the other
/// hooks through <see cref="TransferHookServices.DpopKeySource" />, so that a token one of them fetches is bound to
/// it. A hook that adds such a token sends it as <c>Authorization: DPoP</c> from outside this hook, which then
/// signs the request carrying it.
/// </summary>
public interface IDpopKeySource : ITransferHook
{
    /// <summary>Reads the key.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key, or <see langword="null" /> when it cannot be read; this hook then fails the transfer itself.</returns>
    ValueTask<IDpopKey?> ReadDpopKeyAsync(CancellationToken cancellationToken);
}
