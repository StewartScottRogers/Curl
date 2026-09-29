# ADR-0204 — The hand-built Kerberos reaches `https://` KDCs through an optional MS-KKDCP proxy seam

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-827;
recorded by BL-883. Amends ADR-0168, which skipped `https://` (MS-KKDCP) KDCs until a KDC
proxy client was built.

## Context

A `krb5.conf` realm may name a KDC as `https://host[:port]/path`: a KDC proxy that takes
Kerberos messages over HTTPS (MS-KKDCP), as Active Directory's KDC Proxy Server and MIT's
clients support. MIT's `sendto_kdc.c` wraps each request in a `KDC-PROXY-MESSAGE`
(encoded by `encode_krb5_kkdcp_message`, `asn1_k5.c`), POSTs it, and reads the reply in
`service_https_read`, killing the connection and moving on to the next KDC when the reply
does not decode. BL-827 built the client side in `Curl.Kerberos.UnitLibrary`.

## Decision

- **A separate, optional seam.** `IKerberosKdcProxyTransport.PostAsync(host, port, path,
  body)` makes one HTTPS POST and returns the reply body, throwing `IOException` on
  failure. It is not a new member of `IKerberosKdcTransport`, which would break the
  implementations outside the library (`Curl.Networking`'s `KerberosKdcSocketTransport`,
  `Curl.Console.UnitTests`' fake). `KerberosKdcSender` and `KerberosKdcClient` take it as
  an optional last constructor argument; without it `https://` KDCs are skipped as before.
- **Encoded as MIT encodes it.** `KerberosKdcProxyMessage` follows MS-KKDCP section 2.2.2
  with explicit tags as MIT's `encode_krb5_kkdcp_message` fills it in `sendto_kdc.c`:
  `kerb-message [0]` holds the request framed as over TCP (a four-byte big-endian length,
  then the message), `target-domain [1]` is the realm as a GeneralString, and no
  `dclocator-hint` is sent. Decoding ignores a `dclocator-hint`.
- **A bad proxy reply tries the next KDC.** A reply that is not a `KDC-PROXY-MESSAGE`, or
  whose `kerb-message` length prefix is missing or does not match, is an `IOException`, so
  the realm's next KDC is tried, as MIT's `service_https_read` does.

## Consequences

- Production behaviour is unchanged until a production `IKerberosKdcProxyTransport` is
  supplied and wired in (BL-882: an HTTPS transport in `Curl.Networking.UnitLibrary`,
  composed by `Curl.Console`).
- The encoding is pinned in `KerberosKdcProxyMessageTests` from bytes built by hand from
  the ASN.1, since no MIT build was at hand to record from; `KerberosKdcSenderTests` and
  `KerberosKdcClientTests` drive AS and TGS exchanges through a fake proxy.

## Alternatives considered

- **Add a proxy member to `IKerberosKdcTransport`.** Lost: it breaks every existing
  implementation of the byte transport, in projects outside the task, for a feature most
  realms never use.
- **Send a `dclocator-hint`.** Lost: MIT sends none, and the proxy locates the KDC from
  `target-domain`.
- **Fail the exchange on a malformed proxy reply.** Lost: MIT moves on to the realm's next
  KDC, and so does every other transport failure here.
