# Curl.Authentication.UnitLibrary

Phase 2.

Basic, Digest, NTLM, Negotiate/SPNEGO/Kerberos, Bearer, AWS SigV4, and the SASL
mechanisms the mail handlers use (`SaslAuthenticator`: PLAIN, LOGIN, EXTERNAL, XOAUTH2,
OAUTHBEARER, CRAM-MD5 and DIGEST-MD5, the last as SSPI on Windows; ADR-0121, ADR-0123,
ADR-0139), and the netrc reader (`NetrcFile`: which login and
password curl 8.21.0 picks from `--netrc-file` text, BL-503).

Negotiate (BL-527, ADR-0142, ADR-0173): `RankedHttpAuthenticator.CreateAuthorizationAsync`
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
credential cache, else a TGS exchange). Hand-built NTLM is not composed yet (BL-526); the
hand-built route answers it `NoMechanism`. This library references `Curl.Kerberos.UnitLibrary`.
Tests fake the seam with `ScriptedSecurityContext` and run the hand-built route against
`Curl.Kerberos.UnitTests`' `FakeKdc` and `FakeGssAcceptor`, linked into the test project.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
