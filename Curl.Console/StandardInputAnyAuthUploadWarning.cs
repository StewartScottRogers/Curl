using System.Numerics;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// curl 8.21.0's warning that <c>--anyauth</c> or <c>--proxy-anyauth</c> with an upload from
/// standard input may not work, printed by <c>src/tool_operate.c</c> before a transfer whose
/// <c>-T</c> file is <c>-</c> or <c>.</c> when <c>--proxy-anyauth</c> was given or more than one
/// origin auth scheme is allowed (<c>--anyauth</c>, or <c>--basic --digest</c>), unless <c>-s</c>.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-10-11 (BL-2088): the line wraps as every
/// warning does, at 79 columns into three lines ending <c>instead</c>; <c>-T file</c>,
/// <c>--basic</c> or <c>--digest</c> alone print nothing.
/// </remarks>
internal static class StandardInputAnyAuthUploadWarning
{
    /// <summary>
    /// The warning, unwrapped and without a line terminator.
    /// </summary>
    internal const string Text =
        "Warning: Using --anyauth or --proxy-anyauth with upload from stdin involves a big risk of it not working. "
        + "Use a temporary file or a fixed auth type instead";

    /// <summary>
    /// Whether curl prints <see cref="Text" /> before the transfer, <c>-s</c> aside.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="uploadFile">The URL's <c>-T</c> file, or <see langword="null" /> when it uploads nothing.</param>
    /// <returns><see langword="true" /> when the upload reads standard input and the auth is any-auth.</returns>
    internal static bool AppliesTo(CommandLineOptions options, string? uploadFile) =>
        uploadFile is not null
        && UploadUrl.IsStandardInput(uploadFile)
        && (options.ProxyAuthSchemes == HttpAuthSchemes.Any || BitOperations.PopCount((uint)options.AuthSchemes) > 1);
}
