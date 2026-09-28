# Curl.Authentication.UnitLibrary

Phase 2.

Basic, Digest, NTLM, Negotiate/SPNEGO/Kerberos, Bearer, AWS SigV4, and the SASL
mechanisms the mail handlers use (`SaslAuthenticator`: PLAIN, LOGIN, EXTERNAL, XOAUTH2,
OAUTHBEARER; ADR-0121, ADR-0123).

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
