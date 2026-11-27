# ADR-1461: A redialled connect holds back what its first attempt reported

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6073)

## Context

ADR-1459 repeats the whole connect for the hand-built client, so `-v` showed every line before the
handshake twice. Measured on 2026-11-26: Ubuntu's curl 8.18.0 on OpenSSL 3.5.5, `-sv -k` against
`Record-CurlExchange.ps1 -Tls`, writes one `Trying`, one `ALPN: curl offers`, one
`SSL Trust: peer verification disabled` and one handshake, on one connection. The chain of
ADR-1446 was not measured: the recorder cannot serve a name constraint that does not decode.
OpenSSL reports that error as the result of verifying the one handshake, so it opens no second
connection.

## Decision

- The redialled connect reports through `RedialedConnectTransferEvents`. It holds back everything
  up to and including the trust of the handshake that asked for the redial, which the attempt
  before already reported; everything after it passes. `CONNECT` request and reply heads, the
  header output's copy included, are held back the same way.
- That handshake is counted, not named: the first trust reported, or the second for an origin
  reached through an HTTPS proxy, whose own handshake comes first.
- A redial that ends before that trust writes the lines it held (not the `CONNECT` heads or TLS
  events), so a failed or timed-out second dial is still explained.

Rejected: repeating only the dial under the handshake, which opens a tunnel in a second way beside
the connector's; dropping lines by matching their text, since the second attempt's resolve line
(`was found in DNS cache`) differs from the first's.

## Consequences

- `-v` shows one connection's lines for a redialled `-k` transfer; the server still sees two
  connections.
- Under `--trace-config tcp`, a direct connect's `[TCP]` lines for the redialled handshake's
  records follow those of the abandoned one, as both were read.
