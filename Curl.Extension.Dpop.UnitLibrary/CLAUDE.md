# Curl.Extension.Dpop.UnitLibrary

The DPoP extension (ADR-0999, ADR-1145), tested against RFC 9449. Namespace `Curl.Extension.Dpop`. It references
only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `DpopExtension` gives the `--dpop <key file>` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `DpopTransferHook` sends a fresh DPoP proof with every HTTP request, sends `--oauth2-bearer` as
  `Authorization: DPoP`, keeps each origin's last `DPoP-Nonce` and retries a 400 or 401 that brings a new one once,
  with its `-T` upload and streamed request body rewound (no retry when either cannot be). It runs inside every hook
  that sends several requests or changes a request's method or URL, so each request gets its own proof (ADR-1426,
  ADR-1431); a `grpc` or `grpcs` call's proof names the HTTP `POST` that carries it.
  `DpopAttempt` holds one try's response until the hook keeps it. Its hook is an `IDpopKeySource`: it lends the key
  (`DpopPostProofKey`) to the OAuth2 extension's hook, which runs before it, proves its token requests with that key and
  adds `Authorization: DPoP <token>` for this hook to sign with its `ath` (ADR-1367).
- `DpopProof` builds the proof JWT from a `DpopRequest`; `DpopKey` (`EcDpopKey`, `RsaDpopKey`) signs it and gives
  the public JWK; `DpopKeyFile` reads the PEM key file.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.Dpop.UnitTests`.
