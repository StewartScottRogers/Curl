# ADR-1439: `--dpop` signs inside every hook that repeats or changes a request

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-26
- Task: BL-6042

## Context

ADR-1426 moved the `--dpop` hook inside `--resumable-upload`'s. The hooks still listed after it could send several
requests under one proof (`--webdav-sync`, `--zsync`, `--privacy-pass`) or change the method the proof named
(`--grpc-web`), which RFC 9449 §4.2 forbids: `htm` and `htu` name the request that carries the proof, and `jti` is
unique.

## Decision

- `ExtensionTransferHookCatalogue` lists the DPoP hook after `--record`/`--replay`'s and before the DNS query hook.
  Every hook that sends several requests or changes a method or URL is before it, so each request it sends is signed
  on its own. A later extension of that kind is listed before it too.
- The DNS query and gRPC hooks stay after it: each hands curl's handler a context the handler recognises by its type,
  which an outer DPoP hook would hide. Neither changes the URL or sends a request of its own. The credential store and
  compression dictionary hooks, also after it, each leave the method and URL as the DPoP hook saw them.
- A `grpc` or `grpcs` transfer's proof says `htm` `POST` and an `htu` of scheme `http` or `https`: the request that
  carries it is the HTTP/2 `POST` the gRPC handler makes, whatever `-X` says.
- `--ohttp-relay` stays after it. The proof names the target request, with the target's method and URL, and travels
  inside the encapsulated request to the target resource, which is the server that checks it (RFC 9458 §2). The POST
  to the relay and the key configuration fetch carry no proof and no token: neither is a request for the resource, and
  a proof would tell the relay what Oblivious HTTP hides from it.
- A `DPoP-Nonce` retry rewinds a streamed request body (`-F`, a streamed `-d`) as it rewinds a `-T` upload, and is
  not made when either cannot be rewound.

## Consequences

- The hooks listed before the DPoP hook see the request without its `DPoP` header and without the
  `Authorization: DPoP` header it makes from `--oauth2-bearer`. So `--har` and the generated program of the code
  generation extension do not show them, which also keeps a one-use proof out of a program meant to be run again.
- `--replay` answers a recorded exchange without reading the DPoP key.
- The gRPC server reflection requests the gRPC handler makes without `--grpc-proto` still carry the call's headers,
  and so the call's proof. Signing them needs the handler to ask for a proof per request, filed as a follow-up task.
