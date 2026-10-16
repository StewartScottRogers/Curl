using Curl.Api.EngineContracts;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An easy handle the curl tool's runner performs its serial attempts on (BL-2394, ADR-0552): <c>Curl.Console</c>
/// sets the tool's own settings and host once, the per-transfer hooks before each
/// <see cref="IEasyTransfer.PerformAsync" />, and reads the attempt's whole <see cref="LastResult" />, which its
/// <c>--retry</c> loop, failure lines and <c>-w</c> output read beyond what <see cref="IEasyTransfer.Info" /> carries.
/// <see cref="ManagedEasyTransfer" /> describes what each hook replaces.
/// </summary>
internal interface IToolEasyTransfer : IEasyTransfer
{
    /// <summary>Gets or sets the curl tool's own settings (<see cref="ManagedEasyTransfer.ToolSettings" />).</summary>
    EngineTransferSettings? ToolSettings { get; set; }

    /// <summary>Gets or sets what the transfers reach outside the engine (<see cref="ManagedEasyTransfer.Host" />).</summary>
    EasyTransferHost Host { get; set; }

    /// <summary>Gets or sets the pieces the runner chose before connecting (<see cref="ManagedEasyTransfer.ToolInputs" />).</summary>
    EngineToolTransferInputs? ToolInputs { get; set; }

    /// <summary>Gets or sets where the body and header bytes go (<see cref="ManagedEasyTransfer.ToolOutputs" />).</summary>
    EngineToolTransferOutputs? ToolOutputs { get; set; }

    /// <summary>Gets or sets the runner's events and running transfer (<see cref="ManagedEasyTransfer.ToolEvents" />).</summary>
    EngineToolTransferEvents? ToolEvents { get; set; }

    /// <summary>Gets or sets the runner's progress meter and abort token (<see cref="ManagedEasyTransfer.ToolProgress" />).</summary>
    EngineToolTransferProgress? ToolProgress { get; set; }

    /// <summary>Gets or sets the runner's follower and context factory (<see cref="ManagedEasyTransfer.ToolDispatch" />).</summary>
    EngineToolTransferDispatch? ToolDispatch { get; set; }

    /// <summary>Gets the whole result of the last perform (<see cref="ManagedEasyTransfer.LastResult" />).</summary>
    TransferResult? LastResult { get; }
}
