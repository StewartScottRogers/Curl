# ADR-1319: Curl.Api sets extension options through typed option records

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5617
- Decided by Claude under Stewart's delegation.

## Context

`curl --dns-json` and `--dns-transport` reach the `dns://` extension through the command line's extension option values. `Curl.Api` had no way to set them. Two shapes were open: a typed record per extension (`CurlDnsOptions`) or a generic name/value bag the extension registry validates.

## Decision

Typed records, one per extension, as `Curl.Api` already has one per protocol: `CurlRequest.Dns` holds a `CurlDnsOptions` (`Transport`, `Json`). A typed record is discoverable, checked by the compiler and keeps names and value shapes out of the caller's hands; a bag would make every caller learn option spellings and would need the registry to be reachable from `Curl.Api`.

Underneath, the record maps to `EasyOptions.ExtensionOptions`, a name-to-value dictionary the engine hands to the transfer hooks it was given: `new ManagedCurlEngine(extensionHandlers, extensionTransferHooksFor)`. `CurlClient.Create` passes a function that runs `ExtensionTransferHookCatalogue` activators over those values, as the tool's host does (ADR-1007). The engine still references no extension. A later extension adds its own record and its own mapping onto the same dictionary.

With `CURL_EXTENSIONS=0` the client is created without hooks and refuses a request that sets an extension record with `CurlCode.UnknownOption`, the code for an option the engine does not know, as the tool refuses an unknown option.

## Consequences

Only request-level options exist so far; a client-wide default can be added the same way. `--dns-transport` is not validated by `Curl.Api`: an unknown transport name is refused by the extension when the transfer starts.
