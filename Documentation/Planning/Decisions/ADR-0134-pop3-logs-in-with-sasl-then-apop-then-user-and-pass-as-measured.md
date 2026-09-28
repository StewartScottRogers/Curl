# ADR-0134 — POP3 logs in with SASL, then APOP, then USER and PASS, as measured

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-548.

## Context

ADR-0121 §5 fixed the order of POP3 login (SASL, then `APOP`, then `USER`/`PASS`) and left
`AUTH=+APOP` and the failure texts for BL-548 to measure. Measuring curl 8.21.0 (Schannel)
with `Record-CurlExchange.ps1 -Pop3` on 2026-09-28 (every case in BL-548's Notes) settled
nearly everything. Four points could not be read off a recording, or would cost the
`ISaslExchange` contract a change to match exactly, and are decided here.

## Decision

1. **The login follows the measurements.** SASL `AUTH` when `CAPA` lists a mechanism the
   authenticator chooses; else `APOP` whenever the greeting carries a timestamp (whatever
   `CAPA` says, even when refused); else `USER`/`PASS` when `CAPA` listed `USER` (any case)
   or was refused; else exit 67 `Login denied`. `AUTH=+APOP` allows only `APOP`,
   `AUTH=<mech>` only that mechanism, `AUTH=*` anything; an unknown mechanism or any other
   key is exit 3 before a byte is sent. A bearer token without `-u` tries only SASL. Refusals:
   SASL `Login denied`; `APOP` `Authentication failed: 45` (`-ERR`) or `42` (another `+`
   line); `USER`/`PASS` `Access denied. -` or `Access denied. *`. None sends `QUIT`.
2. **A challenge that is not base64 reaches the exchange as an empty challenge.** curl
   decodes challenges only for mechanisms that read them, so LOGIN answered `+ !!!` with its
   user name (measured). The exchange cannot say whether it reads its challenges, and every
   mechanism built so far (ADR-0123) that ignores them answers the same either way.
3. **`+OK` ends the exchange successfully once the initial response has been sent.** curl
   refuses a `+OK` that arrives before its last message (measured: `+OK` straight after
   `AUTH LOGIN` is exit 67), but `ISaslExchange` does not say how many messages remain. The
   handler refuses `+OK` before the initial response, which covers every single-message
   mechanism exactly; a `+OK` between LOGIN's user name and password is accepted where curl
   refuses it. Matching that needs a "complete" member on the contract, which a later task can
   add if a conformance case needs it.
4. **Of several `AUTH=` options, the last counts; commands and credentials are Latin-1.**
   curl ORs several SASL mechanisms together; `SaslRequest.RequiredMechanism` holds one, and
   nobody writes two. `USER`, `PASS` and the `APOP` digest take the credentials as Latin-1,
   the encoding every POP3 line uses here (BL-547); a non-Latin-1 password is the case
   where curl's command-line bytes could differ.

## Consequences

- `Pop3Login` holds the whole choice; `Pop3LoginOptions` the options; `Pop3ApopDigest` the
  digest, pinned to curl's and RFC 1939's.
- The `+OK`-mid-LOGIN and multiple-`AUTH=` gaps are small and recorded here.

## Alternatives considered

- **Fail a non-base64 challenge with exit 67.** Contradicts the LOGIN recording.
- **Extend `ISaslExchange` with a completion flag now.** A contract change touching every
  protocol task for a case no user hits; not worth serialising the board on.
