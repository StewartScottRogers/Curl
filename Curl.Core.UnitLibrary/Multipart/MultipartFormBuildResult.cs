using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// What <see cref="MultipartFormBodyBuilder.BuildAsync" /> produced: the request body, or the
/// failure curl reports before it sends anything.
/// </summary>
/// <param name="Body">
/// The body, whose <see cref="StreamBody.Content" /> the caller disposes; <see langword="null" />
/// when the build failed.
/// </param>
/// <param name="Failure">The failure; <see langword="null" /> when the build succeeded.</param>
public sealed record MultipartFormBuildResult(StreamBody? Body, TransferResult? Failure)
{
    /// <summary>Gets a value indicating whether <see cref="Body" /> was produced.</summary>
    [MemberNotNullWhen(true, nameof(Body))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsBuilt => Body is not null;
}
