---
id: BL-5881
title: Fix fitness finding STD-200: IMAP and POP3 SASL DIGEST-MD5 fails against Dovecot because the quoted algorithm="md5-sess" is not recognised, so default login fails whenever DIGEST-MD5 is offered
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslDigestMd5.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5881 — Fix fitness finding STD-200: IMAP and POP3 SASL DIGEST-MD5 fails against Dovecot because the quoted algorithm="md5-sess" is not recognised, so default login fails whenever DIGEST-MD5 is offered

## Goal

STD-200 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: IMAP and POP3 SASL DIGEST-MD5 fails against Dovecot because the quoted algorithm="md5-sess" is not recognised, so default login fails whenever DIGEST-MD5 is offered. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (important feature broken) · **Confidence:** Confirmed (more than 4 runs on Windows, 2 on Linux) · **Area:** IMAP / POP3 SASL DIGEST-MD5 (SMTP shares the code; not tested against a real SMTP server). Both platforms.
- **Server:** the Dovecot container from STD-199 (`auth_mechanisms` includes `digest-md5`). Dovecot's challenge is `realm="",nonce="…",qop="auth",charset="utf-8",algorithm="md5-sess"` — `algorithm` and `charset` are quoted, which RFC 2831 permits.
- **Repro:**
  1. `curl -sS -u alice:pass imap://127.0.0.1:36243/` (no login options; DIGEST-MD5 is curl's preferred mechanism here)
  2. `curl -sS -u alice:pass --login-options AUTH=DIGEST-MD5 imap://127.0.0.1:36243/`
  3. `curl -sS -u alice:pass pop3://127.0.0.1:36211/`
- **Expected (REF 8.22.0 Windows, and REF2 8.18 Linux):** `* LIST (\HasNoChildren) "/" INBOX` (IMAP) or the message list (POP3), exit 0. With a wrong password, exit 67 `Login denied`.
- **Actual (.NET):** Windows: `curl: (94) An authentication function returned an error`, exit 94 — it sends `AUTHENTICATE DIGEST-MD5`, receives the challenge, and drops the connection without answering (Dovecot logs "client didn't finish SASL auth"). Linux: `curl: (67) Login denied`. A wrong password also gives 94 on Windows.
- **Likely location:** `Curl.Authentication.UnitLibrary/SaslDigestMd5.cs`: `NonceSspiAccepts` and `AnswerAsCurl` read the algorithm with `ValueAfter(text, "algorithm=", ',')`, which returns `"md5-sess"` including the quotes, so it never equals `md5-sess`; the `charset=utf-8` check misses the quoted form the same way. Strip surrounding quotes from directive values.
- **Acceptance check:** against the Dovecot container, `curl -sS -u alice:pass imap://127.0.0.1:36243/` prints `* LIST (\HasNoChildren) "/" INBOX` and exits 0 on Windows and Linux; the same for `pop3://127.0.0.1:36211/` and `--login-options AUTH=DIGEST-MD5`; `-u alice:wrong` exits 67.

## Acceptance criteria

- [ ] Given Dovecot's exact challenge, `realm="",nonce="<n>",qop="auth",charset="utf-8",algorithm="md5-sess"`, `SaslDigestMd5` answers it (it does not drop the connection), on both the SSPI path (`NonceSspiAccepts`) and the managed path (`AnswerAsCurl`). Unit tests in `Curl.Authentication.UnitTests` pin it with `algorithm` and `charset` both quoted and bare, as RFC 2831 permits either.
- [ ] An IMAP and a POP3 protocol test with a test-double server that sends that challenge log in with DIGEST-MD5 and exit 0. With a server that refuses the response, they exit 67.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-27, requeue after two timeouts (120 min plus 30 min of overtime): do not run Docker or Dovecot. A lane cannot, and the criteria above no longer ask for it. The fix is in parsing. `ValueAfter(text, "algorithm=", ',', …)` at `SaslDigestMd5.cs` lines 45 and 107 returns `"md5-sess"` with its quotes, so it never equals `md5-sess`. Read `algorithm` (and `charset`) as either a quoted or a bare value, for example with the existing `QuotedValueAfter` first, falling back to the bare form. Then pin both forms with unit tests. The original live check against Dovecot is left to the next outside fitness review.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: b956eeec 69116afe 3d836d5e.
- 2026-11-24: Doing -> Blocked. Stewart: dark factory timed out after 120 min and again after 30 min of overtime; see Z:\repos\Curl.logs\BL-5881-20261124-120053-L13.jsonl
- 2026-11-27: Blocked -> Backlog. Requeued 2026-11-27 with unit-test criteria and the parsing fix named; no Docker.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 8ade6e19 35f703e6 3d836d5e.
