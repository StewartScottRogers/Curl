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
  back unchanged when no hook is active.
- `Connection/IConnectionHook` supplies the connector (and optionally the datagram connector) a
  transfer runs over in place of curl's, given curl's so it can reach a first hop through it.
  `ConnectionHooks.ActivatedBy` keeps the one hook whose option was given and throws an
  `ArgumentException` naming every option when two or more were; a host passes `ConnectorOver` and
  `DatagramConnectorOver` to the engine (ADR-1010), which keep curl's connectors when no hook is active.
- `Resolution/IResolverHook` supplies the `IDnsResolver` a transfer looks host names up with in place
  of curl's (DNS-over-TLS, DNS-over-QUIC, Oblivious DoH), given curl's. `ResolverHooks.ActivatedBy`
  keeps the one hook whose option was given and throws an `ArgumentException` naming every option when
  two or more were, or one was beside `--doh-url`; a host passes `ResolverOver` to the engine's
  `extensionResolver` (ADR-1011), which keeps curl's resolver when no hook is active.

Later groundwork adds certificate hooks in their own subfolder.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Abstractions.UnitTests`.
