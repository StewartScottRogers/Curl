using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// The <c>--user-from-keychain</c> transfer hook (ADR-1144): reads the credential stored under the name given and
/// runs the transfer with it as though <c>-u user:password</c> had been given, in place of any <c>-u</c> did. With
/// no credential under that name the transfer fails with exit 67 before it connects.
/// </summary>
/// <param name="store">The OS credential store.</param>
/// <param name="name">The credential's name.</param>
internal sealed class OsCredentialStoreTransferHook(ICredentialStore store, string name) : ITransferHook
{
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var credential = await store.FindAsync(name, context.CancellationToken);
        return credential is null
            ? TransferResult.Failure(CurlExitCode.LoginDenied, $"No credential named {name} in the {store.Name}")
            : await next(new CredentialedTransferContext(context, credential));
    }
}
