using Curl.Protocol.Abstractions;

namespace Curl.Extension.PluginProtocol;

/// <summary>
/// Serves each scheme a <c>curl-proto-&lt;scheme&gt;</c> program was found for by running that program and talking
/// to it over its standard input and output (ADR-1129).
/// </summary>
/// <param name="programs">The helper programs' paths by lowercase scheme.</param>
/// <param name="starter">Starts a helper.</param>
public sealed class HelperProtocolHandler(IReadOnlyDictionary<string, string> programs, IHelperProcessStarter starter) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = [.. programs.Keys];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string program = programs[context.Url.Scheme.ToLowerInvariant()];
        string helperName = Path.GetFileName(program);
        IHelperProcess helper;
        try
        {
            helper = starter.Start(program, context.Url.OriginalString);
        }
        catch (IOException failure)
        {
            return TransferResult.Failure(CurlExitCode.CouldntConnect, $"Failed to run {helperName}: {failure.Message}");
        }

        await using (helper.ConfigureAwait(false))
        {
            return await ExchangeAsync(new HelperExchange(helper, context, helperName)).ConfigureAwait(false);
        }
    }

    private static async ValueTask<TransferResult> ExchangeAsync(HelperExchange exchange)
    {
        try
        {
            return await exchange.RunAsync().ConfigureAwait(false);
        }
        catch (HelperProtocolException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message, failure.BytesTransferred);
        }
        catch (IOException failure)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, $"Failure talking to {exchange.HelperName}: {failure.Message}");
        }
    }
}
