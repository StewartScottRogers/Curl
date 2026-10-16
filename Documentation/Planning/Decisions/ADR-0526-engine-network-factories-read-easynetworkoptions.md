# ADR-0526: The engine's connection, DNS and timeout factories read EasyNetworkOptions

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2323 (ADR-0506 plan phase 1 step 4)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0506 moves the engine from the curl tool's `CommandLineOptions` to the `Curl.Api` option
groups. BL-2303 built `EasyOptionsFromCommandLine.NetworkOf` in `Curl.Console` and ADR-0513
settled what the network record holds. BL-2323 retargets the engine members that read network
settings. Three choices came up.

## Decision

1. `EngineComposition`'s `CreateTransports`, `CreateDnsResolver`, `CreateTcpConnector`,
   `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take the network record beside
   `CommandLineOptions`, which they still read for the TLS, proxy, trace, HTTP version and `-m`
   groups until BL-2304 to BL-2309 move those. `CreateUdpDatagramConnector`, `LocalBindingOf`,
   `UnixSocketOf`, `AddressFamilyOf`, `DnsServerVariableResolverOf` and the new
   `TcpSocketOptionsOf` take the record alone; `ConnectTimeoutOf` and `TransferTimers.Of` take it
   with `-m` as a `TimeSpan?`. `CreateDohConnector` reads no network option, so it is unchanged.
   `TransferContextFactory`'s connect timeout and `--connect-to` fields move in BL-2343.
2. `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests` link `Curl.Console/EasyOptionsFromCommandLine.cs`,
   so their tests that run a whole command line through the transports keep doing so with the same
   mapping production uses. Tests of a single option reader (`LocalBindingOf`, `UnixSocketOf`,
   `AddressFamilyOf`) build the record directly.
3. `--happy-eyeballs-timeout-ms 0` now gives the 200 ms default, as the record says (ADR-0513):
   curl 8.22.0's `--libcurl` writes no `CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS` for 0 (measured), so
   libcurl keeps 200 ms; the engine had raced the families with no head start. A `--local-port`
   whose first port is 0 binds no port, as libcurl takes `CURLOPT_LOCALPORT` 0 as any port.

## Consequences

The engine reads the network group from `Curl.Api`'s record, so `Curl.Api` and `Libcurl` can hand
it one without a command line. `Curl.Engine.UnitLibrary` references `Curl.Api.UnitLibrary`.
