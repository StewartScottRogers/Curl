# ADR-0171 — The hand-built GSS-API Kerberos initiator sends MIT's flags and reads RFC 4121 and RFC 4757 tokens in strict sequence

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-691.

## Context

ADR-0142's hand-built route (no system GSS-API library) needs the Kerberos V5 GSS-API
mechanism for an initiator: the initial context token, the check of the acceptor's AP-REP,
and Wrap and MIC tokens for SASL `GSSAPI`'s security layer (RFC 4752) and SOCKS5's message
protection (RFC 1961). RFC 4121 and RFC 4757 leave the initiator several choices: which
flags go in the checksum, what goes in the authenticator, which key encrypts a forwarded
ticket, which EC and RRC to send, and how strictly to check the acceptor's sequence
numbers. MIT krb5, which curl's GSS-API build links off Windows, is the reference; no
machine with MIT and a KDC was at hand, so the choices below follow MIT's source
behaviour as RFC 4121 describes it and are pinned by tests against an in-memory acceptor
written independently from the RFCs.

## Decision

- **Where it lives.** `KerberosGssContext` in `Curl.Kerberos.UnitLibrary`, over a
  `KerberosCredential` (the service ticket from `KerberosKdcClient`), a
  `KerberosGssContextOptions`, `TimeProvider` and `IKerberosRandomSource`.
  `NextToken(incoming)` is shaped like ADR-0142's `ISecurityContext.NextToken`, and
  `Wrap`/`Unwrap` like its per-message calls, so BL-527's hand-built adapter wraps it one
  to one. Failures are `KerberosGssException` with a `KerberosGssError`:
  `MalformedToken`, `AcceptorError` (with the KRB-ERROR code), `MutualAuthenticationFailed`,
  `IntegrityCheckFailed`, `BadSequenceNumber`.
- **Flags.** The caller's requested flags (curl asks for mutual authentication and
  replay detection, the default), with confidentiality and integrity always added, as
  MIT adds them unless told not to: `0x36` without delegation, `0x37` with it. MIT's local
  `GSS_C_TRANS_FLAG` is not an RFC 4121 checksum flag and is not sent.
- **Delegation.** `--delegation none` never delegates; `always` delegates; `policy`
  delegates only when the service ticket has `ok-as-delegate` (MIT's
  `GSS_C_DELEG_POLICY_FLAG`). Delegation also needs a forwarded ticket-granting ticket in
  the options; without one the flag is dropped silently, as MIT drops it when its TGT is
  not forwardable. The KRB-CRED holds that ticket and its key, with the authenticator's
  time, and is encrypted in the service ticket's session key (key usage 14), not the
  subkey, as MIT does for Windows' sake. Getting the forwarded TGT from the KDC is the
  caller's (filed as its own task).
- **Authenticator.** RFC 4121's checksum (type `0x8003`, channel bindings all zero, as
  when no bindings are passed), a fresh subkey of the session key's type, and a random 30-bit
  initial sequence number, as MIT's `krb5_generate_seq_number` masks it. AP options carry
  `mutual-required` exactly when mutual authentication is requested.
- **AP-REP.** It must decrypt in the session key (usage 12) and echo the authenticator's
  time to the microsecond; a KRB-ERROR token (`03 00`) is `AcceptorError`. The context key
  is the acceptor's subkey when the AP-REP asserts one, else the initiator's subkey; the
  acceptor's numbering starts at the AP-REP's sequence number, or 0 when it gives none.
  Without mutual authentication the context is established after the first token and the
  acceptor's numbering starts at the initiator's, as MIT does.
- **Token formats by the context key's type.** `rc4-hmac` uses RFC 4757 section 7's
  framed RFC 1964 layout (big-endian sequence number, one byte of padding sent, 1 to 8
  accepted when it fits); every other type uses RFC 4121 section 4.2. For RFC 4121 the
  initiator sends EC 0 when encrypting (MIT's padding length for the AES types), EC equal
  to the checksum length when not, and RRC 0; it accepts any RRC (Windows sends 28) by
  rotating back.
- **Strict sequence.** Each acceptor token must carry exactly the next expected sequence
  number: a replay, a gap or a reordering is `BadSequenceNumber`, as is an RC4 token whose
  direction bytes are not the acceptor's. MIT tolerates gaps and some reordering within a
  window when only replay detection is asked for; the protocols curl runs over GSS-API
  (SASL's security layer, SOCKS5, the Negotiate handshake) are in-order streams where a
  gap means corruption, so strictness costs nothing and is simpler to verify.
- **Check order.** Framing and header, then integrity, then padding, then sequence, so a
  token that fails integrity never moves the expected sequence number.

## Consequences

- SASL `GSSAPI` (BL-538), SOCKS5 GSS-API (BL-615) and BL-527's hand-built Negotiate route
  can run the Kerberos mechanism with nothing more than a service ticket.
- `--delegation` (BL-631) works on the hand-built route once a forwarded TGT is fetched.
- Channel bindings are not sent yet. Whether curl's GSS-API Negotiate passes TLS channel
  bindings is to be measured, and taking them is filed as its own task.

## Alternatives considered

- **MIT's windowed replay detection.** Rejected: more state for no gain on in-order
  transports, and harder to pin.
- **Encrypting the KRB-CRED in the subkey.** RFC 4121 allows either, but Windows acceptors
  expect the session key, and MIT uses it.
- **Omitting confidentiality and integrity unless asked.** Rejected: MIT always sets them,
  and acceptors (notably SASL servers) rely on them to offer a security layer.
