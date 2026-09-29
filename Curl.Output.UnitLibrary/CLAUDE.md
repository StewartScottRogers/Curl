# Curl.Output.UnitLibrary

Phase 1.

The 76 --write-out variables, progress meter, verbose and trace formatting.

`ParallelProgressMeterText` writes the text of a `-Z` run's combined progress meter as curl 8.21.0's
`progress_meter` in `src/tool_progress.c` does: the header line, and status lines with five-column
sizes (`max5data`) and eight-column times (`time2str`). The figures come from `Curl.Console`
(ADR-0154, BL-521).

`PeerCertificateText` prints one certificate for `%{certs}` as curl 8.21.0 does: it is a
port of `Curl_extract_certinfo` in curl's `lib/vtls/x509asn1.c`, with `DerReader`,
`X509CertificateFields` and `DerText` porting the lenient ASN.1 parser under it (ADR-0054).
Keep it a port: change it only against that source or a measurement of real curl.

`-w %time{format}` has one dialect per C runtime (`WriteOutTimeDialect`):
`WindowsCRuntimeTimeFormat` (ADR-0038) and `GlibcTimeFormat`, a port of curl's `outtime`
rewrite and glibc's `strftime` in the C locale (ADR-0078). The glibc one is checked
against every row of `Curl.Output.UnitTests/Fixtures/glibc-time-format.json`, measured
from real curl 8.21.0; change it only against a new measurement, added to that fixture.

A TLS handshake is worded for `-v` and the trace dumps as the `TlsBackend` the writer is
given (ADR-0085): Schannel's two ALPN lines, or the OpenSSL build's lines from
`OpenSslHandshakeText`, `OpenSslCertificateText`, `OpenSslDistinguishedNameText` (a port
of OpenSSL's `X509_NAME_print_ex` with curl's flags) and `OpenSslSecurityBits` (a port of
`ossl_ifc_ffc_compute_security_bits`). The OpenSSL build's other TLS lines for `-v` and
the trace dumps (where each TLS message is also dumped as `=> Send SSL data` or
`<= Recv SSL data`) come
from `OpenSslMessageText` (`ossl_trace`: `TLSv1.3 (OUT), TLS handshake, Client hello (1):`),
`OpenSslTrustText` (the `SSL Trust` lines) and `OpenSslHostNameText` (`ossl_verifyhost` and
`hostcheck.c`: the `subjectAltName` and `common name` lines). Keep them ports: change them only against OpenSSL's
or curl's source, or a measurement of the OpenSSL build of curl.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
