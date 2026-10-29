# Curl.Extension.JsonRpc.UnitLibrary

The `--jsonrpc` extension (ADR-0999, ADR-1028): JSON-RPC 2.0 calls over HTTP. Namespace `Curl.Extension.JsonRpc`.
Tested against the JSON-RPC 2.0 specification (https://www.jsonrpc.org/specification), not against curl.

- `JsonRpcExtension` holds the `--jsonrpc` `ExtensionOption`, the `ExtensionDescription` and `HookFor`, the transfer
  hook activator; `Curl.Extension.Composition`'s catalogues register each with one line.
- `JsonRpcCallList` reads the option's value into `JsonRpcCall`s: `<method> [params]`, `!` before a method for a
  notification, `;` between the calls of a batch; ids 1, 2, 3 in order for the calls that are not notifications.
- `JsonRpcRequestEncoding` writes one Request object, or an array of them for a batch.
- `JsonRpcTransferHook` sends that body as an `application/json` POST through `JsonRpcRequestContext` and writes the
  replies `JsonRpcReplyPairing` paired with their calls by id, one per line in call order. A transfer that is not
  HTTP runs unchanged.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.JsonRpc.UnitTests`.
