# Curl.Extension.Composition.UnitLibrary

The one place that lists Curl's extensions: what Curl adds beyond upstream curl (ADR-0999).
Namespace `Curl.Extension.Composition`.

- `ExtensionProtocolHandlers.For(readEnvironmentVariable)` gives the factory a host passes to
  `EngineComposition.CreateProtocolHandlers`' `extensionHandlers`, or `null` when `CURL_EXTENSIONS`
  is `0`. `Curl.Console`'s `CurlComposition` passes it in both production compositions.
- `ExtensionCatalogue.Entries` holds one handler factory per extension, one line each. An extension
  task adds its own line and nothing else here; the file is `merge=union` and a shared list file.
- `ExtensionOptionCatalogue.Entries` holds each extension's long command-line option (`ExtensionOption`),
  one line each, also `merge=union` and a shared list file. `ExtensionCommandLineOptions.For(readEnvironmentVariable,
  isCurlLongOption)` gives them (none when `CURL_EXTENSIONS` is `0`) and fails, naming both, when one has the
  name of a curl option or of another extension option (ADR-1006).
- `ExtensionDescriptionCatalogue.Entries` holds each extension's `ExtensionDescription`, one line each, also `merge=union`
  and a shared list file. `ExtensionListing` builds `curl --extensions`' lines and `curl --ai-help extensions`' Markdown
  from them, or says extensions are off when `CURL_EXTENSIONS` is `0` (ADR-1008).
- `ExtensionTransferHookCatalogue.Entries` holds each extension's transfer hook activator (its `ITransferHook`
  when its option was given, else `null`), one line each, also `merge=union` and a shared list file.
  `Curl.Console`'s `CurlComposition.ExtensionTransferHooksOf` passes them to `TransferHooks.ActivatedBy` for each
  option group and hands the engine `TransferHooks.Around`, or `null` when the group activated none (ADR-1007).
- `ExtensionConnectionHookCatalogue.Entries` holds each extension's connection hook activator (its `IConnectionHook`
  when its option was given, else `null`), one line each, also `merge=union` and a shared list file.
  `CurlComposition.ExtensionConnectionHooksOf` passes them to `ConnectionHooks.ActivatedBy` for each option group
  and hands the engine its `ConnectorOver` and `DatagramConnectorOver`, or `null` for both without a hook; two
  hooks are curl's command-line error with exit 2 (ADR-1010, ADR-1012).

Rules (enforced by `VerifyExtensionIsolation` in `Directory.Build.props`):

- Only the hosts and the extensions reference this project; the engine and every `Curl.Protocol.*`
  never do.
- This is the only project that references the `Curl.Extension.<Name>` libraries.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.Composition.UnitTests`.
