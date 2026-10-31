# ADR-1048: An extension scheme hook serves curl's own schemes in place of curl's handler

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4195)

## Context

BL-4112 runs `ws://` and `wss://` as RFC 8441/9220 extended CONNECT when its option is given. Extensions
could add handlers only for schemes of their own (ADR-0999): `ProtocolDispatcher` refuses two handlers for
one scheme, and transfer hooks (ADR-1007) wrap curl's handler but cannot replace it for one scheme alone.
The engine may not reference an extension, and with no extension option given curl's handlers must be
exactly as before.

## Decision

- `Curl.Extension.Abstractions` (folder `Scheme`) holds `ISchemeHook`: the `ExtensionOption` that
  activated it, the `Schemes` it serves, and `HandlerOver(curlHandler, services)`, which gives the
  extension's handler. It gets curl's handler, so it can fall back to it (a server refusing extended
  CONNECT), and the option group's connector and loopback listener (`TransferHookServices`, ADR-1039).
- `SchemeHooks.ActivatedBy` keeps the hook whose option the option group gave; two activated hooks are an
  `ArgumentException` naming both options, which the Console reports as a command-line error with exit 2,
  as two connection hooks are (ADR-1010). `SchemeHooks.Over(curlHandler)` gives curl's handler back
  unchanged without a hook or when the handler serves none of the hook's schemes; otherwise a handler that
  keeps curl's `SupportedSchemes` and sends a transfer whose URL scheme (case-insensitive) is one of the
  hook's to the extension's handler, and every other scheme to curl's.
- `Curl.Extension.Composition`'s `ExtensionSchemeHookCatalogue` lists one activator per line, a shared list
  file marked `merge=union`.
- The Console passes the scheme hook to the engine through the existing `extensionTransferHooks` seam
  (`CurlComposition.ExtensionHandlerHooksOf`): each handler goes through the scheme hook first and the
  transfer hooks around that, so transfer hooks see the extension's transfers too. With
  `CURL_EXTENSIONS=0` no extension option parses, so no hook activates and nothing changes.

## Consequences

- `Curl.Engine` is unchanged: replacing a handler for some schemes needs no new engine parameter.
- Only one extension may take curl's schemes in an option group; a second needs its own option group.
- The extension's handler is built once per curl handler it replaces, when the option group's dispatch is built.
