# Curl.Authentication.UnitLibrary

Phase 2.

Basic, Digest, NTLM, Negotiate/SPNEGO/Kerberos, Bearer, AWS SigV4, and the SASL
mechanisms the mail handlers use (`SaslAuthenticator`: PLAIN, LOGIN, EXTERNAL, XOAUTH2,
OAUTHBEARER, CRAM-MD5 and DIGEST-MD5, the last as SSPI on Windows; ADR-0121, ADR-0123,
ADR-0139), and the netrc reader (`NetrcFile`: which login and
password curl 8.21.0 picks from `--netrc-file` text, BL-503).

Negotiate (BL-527, ADR-0142, ADR-0176): `RankedHttpAuthenticator.CreateAuthorizationAsync`
hands a Negotiate pick to `NegotiateHttpAuthenticator`, which asks an `ISecurityContextFactory`
(the seam in `Curl.Protocol.Abstractions`) for the first token of a context for `HTTP` on the
URL's host. The production factory is `RoutingSecurityContextFactory`: on Windows
`SystemSecurityContextFactory` (the BCL's `NegotiateAuthentication`, SSPI), with Negotiate
wrapped in `SspiNegotiateSecurityContext` so a token that falls back to NTLM counts as no
credentials, as curl's SSPI gets `SEC_E_NO_CREDENTIALS`; elsewhere the system GSS-API through
the same factory with the default credentials, falling back (`FallbackSecurityContext`) to
`HandBuiltSecurityContextFactory` when it answers `Unsupported`. The hand-built route is SPNEGO
(`SpnegoInitialToken`, `SpnegoNegotiationResponse`) over `Curl.Kerberos.UnitLibrary`'s
`KerberosGssContext`, with the service ticket from `KerberosServiceTicketSource` (the default
credential cache, else a TGS exchange). This library references `Curl.Kerberos.UnitLibrary`
and `Curl.Ntlm.UnitLibrary`.

NTLM (BL-526, ADR-0142, ADR-0181): `RankedHttpAuthenticator` hands an NTLM pick, and `--ntlm`
before any challenge, to `NtlmHttpAuthenticator`, both on the first call and through
`ContinueAuthorizationAsync` for a 401 to a request that already sent a credential. It sends
Type 1, then Type 3 for the server's Type 2 from a fresh context stepped through Type 1 (no
context is kept between legs), and nothing after Type 3. The router gives SSPI on Windows and
`HandBuiltNtlmSecurityContext` (curl's own NTLM, over `Curl.Ntlm`'s `NtlmChallengeAnswerer`)
elsewhere; a Type 2 the context cannot answer throws `HttpAuthenticationFailedException`
(exit 94) where the SSPI build is matched, and sends nothing elsewhere.
Message protection (BL-851, ADR-0183): an established context wraps and unwraps
(`ISecurityContext.Wrap`/`Unwrap`) - the BCL's on the system route, `KerberosGssContext`'s on
the hand-built Kerberos route; curl's own NTLM throws `NotSupportedException`, and an unfinished
context `InvalidOperationException`. SSPI's NTLM needs `SecurityContextRequest.MessageProtection`
to negotiate the keys. SASL exchanges are awaited (`GetInitialResponseAsync`, `RespondAsync`).
Tests fake the seam with `ScriptedSecurityContext` and run the hand-built route against
`Curl.Kerberos.UnitTests`' `FakeKdc` and `FakeGssAcceptor`, linked into the test project.

AWS Signature Version 4 (BL-628, ADR-0178): `AwsSigV4Signer` signs an `AwsSigV4Request`
into an `AwsSigV4SigningResult` (header lines, or curl's exit code and message) exactly as
curl 8.21.0's `http_aws_sigv4.c`, quirks included; `AwsSigV4Scope` parses `--aws-sigv4`,
`AwsSigV4Headers` and `AwsSigV4UriEncoding` canonicalize. Not wired into a transfer yet (BL-629).

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
