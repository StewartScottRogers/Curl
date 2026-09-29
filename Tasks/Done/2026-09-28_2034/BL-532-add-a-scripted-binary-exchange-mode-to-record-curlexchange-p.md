---
id: BL-532
title: Add a scripted binary exchange mode to Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-532 — Add a scripted binary exchange mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Script <file>` serves one connection from a script of steps (`read` until N bytes or an idle gap, `send` escaped bytes, `close`), so binary request-reply protocols such as LDAP's BER messages and SMB's frames can be measured against the reference curl, with every byte curl sent written to `request.bin` and a hex transcript to `transcript.txt`.

## Context

- Needed to measure `ldap://` (audit row 37) and `smb://` (row 39) before any bytes are pinned. The HTTP mode reads until `CRLF CRLF`, which binary protocols never send.
- Reuse the escape syntax `-Response` already documents (`\xHH` and friends) and the `-Tls` certificate for `ldaps://`.

## Acceptance criteria

- [x] `.PARAMETER Script` documents the step syntax with an example, in the script header.
- [x] A script that reads one message and sends a canned LDAP `BindResponse` (success) then reads the search request and closes records the reference curl's `ldap://127.0.0.1:<P>/dc=example` bind and search bytes in `request.bin`.
- [x] A `read` step that times out ends the session and is noted in `transcript.txt`, without hanging the script.
- [x] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

- Step syntax (defaults taken, rule 1): `read` (first byte within `-ScriptIdleMilliseconds`, default 5000, then until silent for `-ScriptGapMilliseconds`, default 250), `read <N>`, `read ber` (one whole definite-length BER element, which is what an LDAP message is and what makes LDAP scripts deterministic), `send <escaped bytes>` with `-Response`'s escapes, `close`; `#` comments and blank lines skipped. Bytes beyond what a read asked for wait for the next read. Transcript lines are `> `/`< ` plus lowercase hex pairs, and `= ` lines for how the session ended. `-Tls` serves TLS from the first byte (ldaps://). Reads use `ReadAsync` + `Task.Wait(timeout)`, so a timeout leaves the stream usable and works the same over `SslStream`.
- Measured, the Windows reference (curl 8.21.0, WinLDAP) opens **two** connections: curl's own, which stays silent, and WinLDAP's, which carries the LDAP traffic. So when the script starts with a read, the recorder accepts every connection and serves the first one with bytes waiting; transcript.txt notes the count.
- After a session ended, WinLDAP reconnected into the listener's backlog and waited minutes, so curl never exited. The scripted server now stops the listener when its session ends; a reconnect is refused at once and curl exits (3 s instead of 4 min).
- Criterion 2 needs `-u cn=u:p --basic`: then WinLDAP's first message is a simple BindRequest (id 1, v3, `cn=u`, `p`), then the SearchRequest for `dc=example` (id 2, scope base, `(objectClass=*)` as `87 0b ObjectClass`), then UnbindRequest. Without `-u`, curl binds through `ldap_win_bind` with the logged-on user's credentials: WinLDAP first searches the rootDSE for `supportedCapabilities` and `supportedSASLMechanisms`, then sends a Sicily/NTLM negotiate bind (`8a 40 NTLMSSP...`, carrying the machine's workgroup name), and a plain success BindResponse does not satisfy it (exit 38 "bind via ldap_win_bind Server Down"). That is for the LDAP measurement task (BL-585) to pin, not this one. The criterion's recording: `curl -sS -u cn=u:p --basic ldap://127.0.0.1:18398/dc=example` exited 39 ("LDAP remote: Server Down", after WinLDAP's 30 s wait for the search result) with both messages in request.bin.
- A `read ber` that got no byte for 3000 ms ended the session with `= read ber timed out after 3000 ms with 0 bytes; session ended`, and the whole run took 3.6 s.
- Before/after check: the HEAD version and the new version recorded the same HTTP POST (`-sS -d x=1`) and the same FTP upload (`-T`) byte for byte in every file, with only the ephemeral EPSV port masked in transcript.txt.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Script serves one connection from read/send/close steps, recording binary exchanges such as curl's LDAP bind and search
