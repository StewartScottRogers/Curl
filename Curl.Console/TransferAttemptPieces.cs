using Curl.Api.Options.Behaviour;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// What the runner chose for every attempt of one transfer (BL-2394): the URL it checked, the transfer options with
/// the transfer's <c>-T</c> upload, and how to build each attempt's outputs and inputs. A serial attempt hands them to
/// its easy handle as <see cref="ManagedEasyTransfer.ToolOutputs" /> and <see cref="ManagedEasyTransfer.ToolInputs" />;
/// a <c>-Z</c> attempt builds its context from them directly.
/// </summary>
/// <param name="Url">The transfer URL, parsed and checked.</param>
/// <param name="Transfer">The transfer options, with the transfer's <c>-T</c> upload (<c>TransferOptionsOf</c>).</param>
/// <param name="Outputs">Builds an attempt's body chain, <c>-D</c> stream, <c>-J</c> watcher and styles, fresh for each attempt.</param>
/// <param name="Inputs">Gives an attempt's pieces from the <c>-C</c> offset it resumes from.</param>
internal sealed record TransferAttemptPieces(
    CurlUrl Url,
    EasyTransferOptions Transfer,
    Func<EngineToolTransferOutputs> Outputs,
    Func<long?, EngineToolTransferInputs> Inputs);
