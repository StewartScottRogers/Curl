# ADR-1454: `--dpop` signs each gRPC server reflection request through a signer on the transfer

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-26
- Task: BL-6055

## Context

Without `--grpc-proto` the gRPC handler makes a server reflection request before the call, on the same connection,
with the call's headers. Those held the call's one DPoP proof (ADR-1439), so the reflection request carried a proof
naming another URL and reused its `jti`, which RFC 9449 §4.2 forbids. The proof is made by the `--dpop` hook, and
only the handler knows that it makes a further request, and where to.

## Decision

- `Curl.Extension.Abstractions` gains `RequestHeadersSigner`: given a further request's method, URL and headers, it
  gives the headers that request carries. A hook puts one on the transfer with `WithRequestSigner`; a handler asks
  `SignedHeadersFor` for each further request. With no signer the headers are unchanged.
- The signer is found by walking the contexts that wrap one another (`IWrappingTransferContext`), because hooks listed
  after `--dpop`'s wrap the context again (`--user-from-keychain`, `--compression-dictionary`). Every signer found is
  applied, each replacing only what it signed itself. A later hook listed after `--dpop`'s that wraps a context
  implements `IWrappingTransferContext`.
- The DPoP signer replaces the proof header it added, and no other (a `-H "DPoP: ..."` stays as given), with a new
  proof: the further request's method and URL, a new `jti`, the same `ath`, and the last nonce its origin sent.
- The gRPC handler signs the reflection stream's headers for `POST <scheme>://<authority>/<service>/<method>`. One
  stream is one HTTP request, so it carries one proof however many reflection messages it exchanges.

A property on `HttpRequestOptions` would have survived any wrapper, but would put an extension's concern in curl's
own protocol contract, which no curl handler would read.

## Consequences

- A server that checks `htu` and `jti` accepts the reflection request and the call.
- A reflection request refused for a missing `DPoP-Nonce` is still not retried: the reflection failure reports no
  response headers to the hook. Filed as a follow-up task, done by ADR-1455.
