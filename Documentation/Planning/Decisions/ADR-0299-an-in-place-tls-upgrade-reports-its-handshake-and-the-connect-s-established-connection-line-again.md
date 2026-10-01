# ADR-0299 — An in-place TLS upgrade reports its handshake and the connect's Established connection line again

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1058.

## Context

curl 8.21.0 (mingw, Schannel), `-v -k --ssl-reqd --mail-from a@b --mail-rcpt c@d -T mail.txt
smtp://127.0.0.1:18027/client` against `Record-CurlExchange.ps1 -Smtp` (BL-546 Notes), writes
between `< 220 Ready to start TLS` and the second `> EHLO client`:

```
* schannel: disabled automatic use of client certificate
* schannel: using IP address, SNI is not supported by OS.
* Established connection to 127.0.0.1 (127.0.0.1 port 18027) from 127.0.0.1 port 53681 
```

The last line repeats the connect's own line, trailing space included: curl adds an SSL
filter to the connection's filter chain and announces the chain connected again once the
handshake is done.

Curl wrote none of the three. `ITlsProvider.AuthenticateAsClientAsync(plaintext, host, token)`
took no `ITransferEvents`, so a STARTTLS handshake reported nothing, and a protocol handler
never sees the connect's `ConnectionOpenedEvent`: `TcpConnector` reports it on the target's
events and returns only the connection.

The two `schannel:` lines are not STARTTLS's: the Schannel build writes the first before
every handshake, HTTPS's included (ADR-0046), and Curl writes it for none.

## Decision

1. `ITlsProvider` gains a default member, `AuthenticateAsClientAsync(plaintext, targetHost,
   events, token)`, that runs the three-argument handshake and reports nothing. Both
   production providers already had a public method with that signature, which reports the
   trust and the handshake with no ALPN offered, so they implement it unchanged. Every
   protocol's fake provider keeps compiling and reporting nothing.
2. `ConnectionOpenedCapturingTransferEvents` (Curl.Protocol.Abstractions) passes every
   event on and keeps the last first-connection `ConnectionOpenedEvent`. It lives beside the
   seam, not in SMTP, so IMAP, POP3 and FTP can use it too.
3. `SmtpProtocolHandler` wraps its connect target's events in one. After an accepted
   `STARTTLS`, `SmtpSession` passes the transfer's events to the handshake and, once it
   succeeds, reports the kept event again, so `-v` and the trace dumps write the second
   `Established connection` line. A connect that reported nothing gets no second line.
4. The `schannel:` lines are filed as BL-1083, for every handshake at once, because they are
   the TLS client's wording, not SMTP's. IMAP, POP3 and FTP are filed as BL-1084.

## Consequences

- An SMTP STARTTLS `-v` matches curl's apart from the two `schannel:` lines, which BL-1083
  adds. On the OpenSSL build the upgrade now also writes that build's trust and handshake
  lines, as an HTTPS connect does. That build's STARTTLS output has not been measured on
  Linux; BL-1083 and BL-1084 measure it before pinning anything for it.
- Reporting the same `ConnectionOpenedEvent` twice keeps the transfer's `%{conn_id}`: the
  connection-id recorder maps the same connection number again.
- A `ConnectTarget` built by the SMTP handler no longer holds the transfer's events object
  itself, only a sink that forwards to it. Tests that compared whole targets compare them
  with `Events` reset.
