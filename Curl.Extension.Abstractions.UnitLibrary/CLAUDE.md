# Curl.Extension.Abstractions.UnitLibrary

The contract for extensions that need more than a URL scheme (ADR-0999). Namespace
`Curl.Extension.Abstractions`. Every extension may reference it; it references only `Curl.Protocol.Abstractions`, whose types its transfer, connection and resolver hooks use.

- `ExtensionOption` is one long command-line option an extension adds: its name without `--`, its
  `ExtensionOptionArgument` (`None` for a flag, `Value` for one value) and its one-line help. An
  extension declares it in one file and adds one line for it to `Curl.Extension.Composition`'s
  `ExtensionOptionCatalogue` (ADR-1006).
- `ExtensionOptionValues` is what one option group gave those options, which the extension reads at
  transfer time: `IsGiven` and `ValueOf` (the empty string for a flag, the last value given wins).
- `Transfer/ITransferHook` is what an activated extension does to one transfer: request headers added
  after curl's, a stream the response body passes through before curl's output, and the finished
  `TransferResult` (status, headers, timings). `TransferHooks.ActivatedBy` keeps the hooks whose option
  was given; a host passes `TransferHooks.Around` to the engine (ADR-1007), which gives each handler
  back unchanged when no hook is active. Its `ExecuteAsync(context, next)` runs the transfer, the hooks
  outermost first in request order and curl's handler innermost (ADR-1019): a hook may skip `next` and
  write its own response and `TransferResult` (a fresh cached response), or call `next` with
  `context.WithOutputs(...)` (`TransferContextOutputs`) to capture curl's response and write a different
  one (a stored response for a 304), or with `context.WithRequest(url, method, body)`
  (`TransferContextRequests`) to send a request of its own (a WebDAV `PROPFIND`) through curl's HTTP stack; a
  null method or body keeps the transfer's own. By default it calls `next` unchanged.
- `Connection/IConnectionHook` supplies the connector (and optionally the datagram connector) a
  transfer runs over in place of curl's, given curl's so it can reach a first hop through it.
  `ConnectionHooks.ActivatedBy` keeps the one hook whose option was given and throws an
  `ArgumentException` naming every option when two or more were; a host passes `ConnectorOver` and
  `DatagramConnectorOver` to the engine (ADR-1010), which keep curl's connectors when no hook is active.
- `Resolution/IResolverHook` supplies the `IDnsResolver` a transfer looks host names up with in place
  of curl's (DNS-over-TLS, DNS-over-QUIC, Oblivious DoH), given curl's. `ResolverHooks.ActivatedBy`
  keeps the one hook whose option was given and throws an `ArgumentException` naming every option when
  two or more were, or one was beside `--doh-url`; a host passes `ResolverOver` to the engine's
  `extensionResolver` (ADR-1012), which keeps curl's resolver when no hook is active.
- `Certificates/ICertificateCheckHook` adds a check to a server's certificate chain after curl's own
  verification accepted it (DANE/TLSA, Certificate Transparency, ML-DSA): it returns `null` to let the
  chain stand or the message the transfer fails with as exit 60. `CertificateCheckHooks.ActivatedBy` keeps
  every hook whose option was given, run in order until one refuses; a host passes `ConnectorOver` and
  `TlsProviderOver` to the engine's `extensionCertificateConnector` and `extensionTlsProvider` (ADR-1016).
  A chain curl refused, a reused connection and a connection without TLS pass through unchecked, so a
  check can only add a refusal.
- `Arguments/IArgumentExpander` turns one extension option's value into curl command-line arguments (URLs, `-X`,
  `-H`, `--data-binary`, `--next`), so an extension can supply whole requests. `ArgumentExpansion.Expand` replaces
  each expander's `--name value` or `--name=value` in the top-level arguments with them (not after a bare `--`, not in
  `-K` files, and not again in an expansion); a host calls it before parsing (ADR-1040). The expander reads files and
  the environment through `ArgumentExpansionContext`'s delegates, and throws `ArgumentExpansionException` to end the
  run with `curl: <message>` and its `CurlExitCode` before any transfer.
- `Arguments/ITrailingArgumentExpander` is an `IArgumentExpander` that also takes what follows its option in the same
  `--next` group (ADR-1051): the values of its `CompanionOptions` and the positional arguments its
  `ClaimsPositionalArgument` accepts, such as `--openapi s --operation getPet petId=5`. `ArgumentExpansion.Expand`
  holds its option until the group ends, removes those arguments, and hands them to
  `ArgumentsFor(ClaimedArguments, ...)` beside the value, each list in command-line order (`CompanionOptionValue`s
  and strings). A positional argument it does not claim stays a URL, and one right after an option that may take it
  as its value (`-d a=1`) is never claimed.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Abstractions.UnitTests`.
