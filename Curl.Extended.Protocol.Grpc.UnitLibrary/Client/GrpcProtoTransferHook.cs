using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// The <c>--grpc-proto</c> option's hook (ADR-1147): gives a <c>grpc://</c> or <c>grpcs://</c> transfer to the gRPC
/// handler as a <see cref="GrpcProtoContext" /> carrying the <c>.proto</c> file; every other transfer passes through unchanged.
/// </summary>
/// <param name="protoPath">The <c>--grpc-proto</c> file.</param>
internal sealed class GrpcProtoTransferHook(string protoPath) : ITransferHook
{
    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        next(GrpcProtocolHandler.Serves(context.Url) ? new GrpcProtoContext(context, protoPath) : context);
}
