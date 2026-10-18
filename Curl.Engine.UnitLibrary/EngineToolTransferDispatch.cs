using Curl.Core;

namespace Curl.Engine;

/// <summary>
/// The curl tool's runner's follower and context factory for one transfer, which a <see cref="ManagedEasyTransfer" />
/// given them through <see cref="ManagedEasyTransfer.ToolDispatch" /> performs through in place of the dispatcher its
/// own factory builds and its own <see cref="TransferContextFactory" /> (BL-2479, ADR-0614). <see cref="Follower" /> is
/// the runner's <see cref="RedirectFollower" /> over its option group's <c>TransferDispatch</c> - its handlers,
/// connectors, connection pool over the run's shared cache, cookie store and <c>--resolve</c> loader - and
/// <see cref="ContextFactory" /> the runner's own factory, with its standard input for a <c>telnet</c> upload, the
/// platform's command-line text encoding, the run's diagnostic log and its HSTS store already set.
/// </summary>
/// <param name="Follower">Performs the transfer, following redirects, through the runner's dispatcher.</param>
/// <param name="ContextFactory">Builds the transfer's context.</param>
internal sealed record EngineToolTransferDispatch(RedirectFollower Follower, TransferContextFactory ContextFactory);
