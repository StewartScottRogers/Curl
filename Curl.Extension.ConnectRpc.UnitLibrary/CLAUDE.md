# Curl.Extension.ConnectRpc.UnitLibrary

The `--connect-rpc` extension (ADR-0999, ADR-1127): Connect protocol unary and streaming calls over HTTP with the
JSON codec. Namespace `Curl.Extension.ConnectRpc`. Tested against the Connect protocol reference
(https://connectrpc.com/docs/protocol), not against curl.

- `ConnectRpcExtension` holds the `--connect-rpc <unary|stream>` `ExtensionOption`, the `ExtensionDescription` and
  `HookFor`, the transfer hook activator; `Curl.Extension.Composition`'s catalogues register each with one line.
- `ConnectTransferHook` sends the `-d` message (`{}` when none) as the request: a unary call as `application/json`,
  writing the response unchanged; a streaming call as `application/connect+json` envelopes, writing the reply
  messages one per line and failing with exit 22 when the end-of-stream message carries an error. Both send
  `Connect-Protocol-Version: 1`, and `Connect-Timeout-Ms` with `--max-time`. `ConnectRequestContext` gives curl's
  handler that request. A transfer that is not HTTP runs unchanged.
- `ConnectRequestMessages` splits a streaming call's body into JSON values; `ConnectEnvelope` frames and reads
  envelopes (`ConnectEnvelopedMessage`); `ConnectStreamReply` reads a streaming response.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.ConnectRpc.UnitTests`.
