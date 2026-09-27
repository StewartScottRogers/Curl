# Curl.Output.UnitLibrary

Phase 1.

The 76 --write-out variables, progress meter, verbose and trace formatting.

`PeerCertificateText` prints one certificate for `%{certs}` as curl 8.21.0 does: it is a
port of `Curl_extract_certinfo` in curl's `lib/vtls/x509asn1.c`, with `DerReader`,
`X509CertificateFields` and `DerText` porting the lenient ASN.1 parser under it (ADR-0054).
Keep it a port: change it only against that source or a measurement of real curl.

`-w %time{format}` has one dialect per C runtime (`WriteOutTimeDialect`):
`WindowsCRuntimeTimeFormat` (ADR-0038) and `GlibcTimeFormat`, a port of curl's `outtime`
rewrite and glibc's `strftime` in the C locale (ADR-0078). The glibc one is checked
against every row of `Curl.Output.UnitTests/Fixtures/glibc-time-format.json`, measured
from real curl 8.21.0; change it only against a new measurement, added to that fixture.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
