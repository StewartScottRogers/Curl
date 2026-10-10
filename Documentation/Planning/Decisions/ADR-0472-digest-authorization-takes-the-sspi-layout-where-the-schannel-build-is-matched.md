# ADR-0472: a Digest Authorization value takes SSPI's layout where the Schannel build is matched

- Status: Accepted
- Date: 2026-10-10
- Decided by Claude under Stewart's delegation.
- Task: BL-2022 (wiring in `Curl.Console`: BL-2033)

## Context

curl 8.21.0's Schannel build answers HTTP Digest through SSPI, not its own `vauth/digest.c`,
and SSPI lays the `Authorization` value out differently. Measured on Windows with
`Record-CurlExchange.ps1` and `curl --digest -u u:p http://127.0.0.1:{port}/64`:

- `Digest realm="r", nonce="abc"`: `username="u",realm="r",nonce="abc",uri="/64",response="fc3de222db74c3ec88aabb5510c76f80"`.
- with `opaque="op", algorithm=MD5`: `...,uri="/64",algorithm=MD5,response="...",opaque="op"`.
- with `qop="auth"`: `...,uri="/64",cnonce="...",nc=00000001,response="...",qop="auth"`.
- with all three: `...,cnonce="...",nc=00000001,algorithm=MD5,response="...",qop="auth",opaque="op"`.

The hashes match curl's own code; only separators and order differ. The OpenSSL build keeps
`vauth/digest.c`'s `, `-separated form in RFC 7616's order, which `DigestAuthenticator` already
writes (ADR-0025).

## Decision

`DigestAuthenticator` takes `matchesSspiBuild` (default `false`), as `NtlmHttpAuthenticator`
does: `true` writes SSPI's layout - no blank after a comma, `cnonce` and `nc` after `uri`,
`algorithm` before `response`, `qop` quoted after it, `opaque` and then `userhash=true` last.
The composition root sets it on Windows (BL-2033). An opt-in parameter rather than a check of
the platform inside the library keeps every existing test platform-neutral and lets both
forms be pinned by unit tests on any machine.

`userhash` and the SHA-256 family were not measured through SSPI; they keep their own
parameters in the SSPI order above until a measurement says otherwise.

## Consequences

- On Windows Curl's Digest request bytes match curl's Schannel build once BL-2033 wires it.
- Reading a sent answer back (`RepeatAuthorization`) parses either layout.
