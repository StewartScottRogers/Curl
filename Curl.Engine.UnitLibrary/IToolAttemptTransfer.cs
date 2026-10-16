using Curl.Api.EngineContracts;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An easy handle that can perform an attempt the curl tool's runner set up (<see cref="ToolAttempt" />,
/// ADR-0552): <c>Curl.Console</c> sets <see cref="ToolAttempt" />, calls <see cref="IEasyTransfer.PerformAsync" />,
/// and reads the attempt's whole <see cref="ToolAttemptResult" />, which its <c>--retry</c> loop, failure lines
/// and <c>-w</c> output read beyond what <see cref="IEasyTransfer.Info" /> carries.
/// </summary>
internal interface IToolAttemptTransfer : IEasyTransfer
{
    /// <summary>
    /// Gets or sets the attempt the next <see cref="IEasyTransfer.PerformAsync" /> performs in place of one built
    /// from the options; that perform clears it, so a later one without a new attempt runs from the options.
    /// </summary>
    ToolAttempt? ToolAttempt { get; set; }

    /// <summary>Gets the result of the last <see cref="ToolAttempt" /> performed, or <see langword="null" /> before the first.</summary>
    TransferResult? ToolAttemptResult { get; }
}
