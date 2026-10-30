# Curl.Extension.PluginProtocol.UnitLibrary

The helper program extension (ADR-0999, ADR-1129): a URL whose scheme neither curl nor another extension serves
runs through a program named `curl-proto-<scheme>` on `PATH`, git's remote-helper model adapted. Namespace
`Curl.Extension.PluginProtocol`. Tested against gitremote-helpers and ADR-1129, not against curl.

- `PluginProtocolExtension` holds the `ExtensionDescription`, `CurlSchemes` (the schemes a helper never takes) and
  `CreateHandler`, which `Curl.Extension.Composition`'s `ExtensionCatalogue` calls with the other extensions' schemes.
- `HelperProgramSearch` finds the programs in the `PATH` directories (`PATHEXT` on Windows); `HelperProgramDirectory`
  lists one directory's `curl-proto-*` files, excluded from coverage (ADR-0083).
- `HelperProtocolHandler` serves one scheme per program found, starting it through an `IHelperProcessStarter`;
  `HelperExchange` holds the conversation (`capabilities`, `get` or `put`, the `ok` or `error` reply and the body);
  `HelperProtocolException` carries a failure's exit code. `SystemHelperProcessStarter` is the thin `Process`
  adapter, excluded from coverage (ADR-0083).

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.PluginProtocol.UnitTests`.
