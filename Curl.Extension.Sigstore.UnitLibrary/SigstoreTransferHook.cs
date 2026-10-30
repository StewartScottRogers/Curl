using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Sigstore;

/// <summary>
/// The <c>--sigstore-bundle</c> transfer hook (ADR-1101). It runs curl's transfer with the body held back, the headers
/// written as they arrive, and writes the body only when <see cref="SigstoreVerification" /> passes it. A body that fails
/// is not written and the transfer fails with exit code 60; a transfer that fails itself is returned as it is.
/// </summary>
/// <param name="expectation">The bundle, identity, issuer and roots the user gave.</param>
internal sealed class SigstoreTransferHook(SigstoreExpectation expectation) : ITransferHook
{
    /// <summary>Gets what the download is verified against.</summary>
    public SigstoreExpectation Expectation => expectation;

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (expectation.Identity is not { } identity || expectation.RootPath is not { } rootPath)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, "--sigstore-bundle needs --sigstore-identity and --sigstore-root");
        }

        if (await ReadBundleAsync(context.CancellationToken) is not { } bundle)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, $"--sigstore-bundle: {expectation.BundlePath} is not a readable Sigstore bundle");
        }

        return ReadRoots(rootPath) is { Count: > 0 } roots
            ? await VerifiedTransferAsync(context, next, bundle, roots, identity)
            : TransferResult.Failure(CurlExitCode.ReadError, $"--sigstore-root: {rootPath} holds no readable PEM certificate");
    }

    private async Task<TransferResult> VerifiedTransferAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next, SigstoreBundle bundle, X509Certificate2Collection roots, string identity)
    {
        using MemoryStream body = new();
        var result = await next(context.WithOutputs(body, context.HeaderOutput, context.DumpHeaderOutput));
        if (!result.IsSuccess)
        {
            return result;
        }

        if (SigstoreVerification.FailureOf(body.GetBuffer().AsSpan(0, (int)body.Length), bundle, roots, identity, expectation.Issuer) is { } failure)
        {
            return TransferResult.Failure(CurlExitCode.PeerFailedVerification, $"--sigstore-bundle: {failure}", result.BytesTransferred);
        }

        body.Position = 0;
        await body.CopyToAsync(context.Output, context.CancellationToken);
        return result;
    }

    private async Task<SigstoreBundle?> ReadBundleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return SigstoreBundle.Parse(await File.ReadAllBytesAsync(expectation.BundlePath, cancellationToken));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static X509Certificate2Collection? ReadRoots(string path)
    {
        try
        {
            X509Certificate2Collection roots = [];
            roots.ImportFromPemFile(path);
            return roots;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            return null;
        }
    }
}
