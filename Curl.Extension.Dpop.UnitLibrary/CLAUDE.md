# Curl.Extension.Dpop.UnitLibrary

The DPoP extension (ADR-0999, ADR-1136), tested against RFC 9449. Namespace `Curl.Extension.Dpop`. It references
only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `DpopExtension` gives the `--dpop <key file>` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `DpopTransferHook` sends a fresh DPoP proof with every HTTP request, sends `--oauth2-bearer` as
  `Authorization: DPoP`, keeps each origin's last `DPoP-Nonce` and retries a 400 or 401 that brings a new one once.
  `DpopAttempt` holds one try's response until the hook keeps it.
- `DpopProof` builds the proof JWT from a `DpopRequest`; `DpopKey` (`EcDpopKey`, `RsaDpopKey`) signs it and gives
  the public JWK; `DpopKeyFile` reads the PEM key file.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.Dpop.UnitTests`.
