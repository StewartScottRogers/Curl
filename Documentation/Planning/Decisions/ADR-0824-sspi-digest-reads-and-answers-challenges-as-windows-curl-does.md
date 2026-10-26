# ADR-0824: Where the SSPI build is matched, Digest reads and answers a challenge as Windows curl's SSPI does

- **Status:** Accepted
- **Date:** 2026-10-26

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3606
(CGR-077 of the third-party gap report).

## Context

On Windows the engine matches curl's Schannel build (`DigestAuthenticator`'s
`matchesSspiBuild`), whose Digest answers come from SSPI, not from curl's own `vauth/digest.c`.
Before this decision the port read a challenge with curl's own parser and only laid the answer
out in SSPI's order (BL-2022), refusing only SHA algorithms (ADR-0791). So it answered
challenges SSPI refuses, and sent `userhash`, curl's `%XX` escapes and the challenge's realm
where SSPI sends something else. Curl 8.22.0 (Schannel, Git for Windows' ucrt64 build) was
measured on 2026-10-26 with `Record-CurlExchange.ps1` over 45 challenge and user name forms;
the results are in BL-3606's Notes.

## Decision

Where the SSPI build is matched, `DigestSspiChallenge` reads the challenge as SSPI does, and
the answer follows what was measured:

- Refused with exit 94: an element without `=`, an empty element or trailing comma, a quoted
  value not closed or followed by more than blanks, no `nonce` or more than one, an algorithm
  other than MD5 and MD5-sess, a `qop` list without `auth`, MD5-sess without a `qop`, and a user
  name holding a control character. An empty user name fails with exit 67, "Login denied".
- Blanks are allowed around keys, `=`, values and commas; other repeated parameters take the
  last value; `userhash` and unknown keys are ignored.
- The realm is the user name's domain (`DOMAIN\user` or `DOMAIN/user`) when it has one, else the
  last `realm` curl's own parser reads (curl hands it to SSPI as the identity's domain), so a
  `realm = "x"` with blanks answers `realm=""`.
- The algorithm is written `MD5` or `MD5-sess` whatever its case; `charset=UTF-8` adds a final
  `charset=utf-8`.
- Only the user name and realm are escaped, a backslash before `"` and `\`; the nonce, `uri` and
  `opaque` are sent as they are, and bytes outside ASCII are sent raw.

The OpenSSL build's form (`matchesSspiBuild` false) is unchanged.

## Consequences

- Every CGR-077 repro matches curl 8.22.0 on Windows, for server and proxy Digest.
- MD5-sess without a `qop` fails with exit 94 where SSPI answers it: SSPI's hash for that answer
  could not be reproduced from the bytes it sends, so the port refuses it rather than send a
  wrong response.
- SSPI sends `nc=00000002` in its first answer when the 401 closed the connection (it sends
  `nc=00000001` on a kept connection); the authenticator keeps no connection state, so that is
  left to BL-3869.
