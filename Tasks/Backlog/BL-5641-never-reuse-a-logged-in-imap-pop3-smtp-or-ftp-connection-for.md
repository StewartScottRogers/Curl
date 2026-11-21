---
id: BL-5641
title: Never reuse a logged-in IMAP, POP3, SMTP or FTP connection for a request with different credentials
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ConnectionPoolKey.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5641 — Never reuse a logged-in IMAP, POP3, SMTP or FTP connection for a request with different credentials

## Goal

A request whose user name or password differs from the logged-in connection's gets a new connection and its own login, as upstream curl does, through `Curl.Api`'s `CurlClient` and through `curl --next`.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 2. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`CurlClient` (and `curl --next`) reuse a logged-in IMAP connection for a request with different credentials, so a wrong token on the second request succeeds. Upstream curl does not reuse a connection whose credentials differ for protocols that log in per connection (`PROTOPT_CREDSPERREQUEST` is off for IMAP, POP3, SMTP, FTP, SMB, LDAP and the SSH schemes; curl's `ConnectionExists` compares user and password for them). The pool key (`Curl.Networking.UnitLibrary/ConnectionPoolKey.cs`) must carry the login (user, password or a hash of it, and the SASL mechanism or `--login-options`) for those schemes, never for HTTP, where credentials go per request. Measure curl 8.22.0 with `--next` and two different `-u` values against IMAP to confirm it opens a second connection.

## Acceptance criteria

- [ ] Two IMAP requests to the same server with different `-u` (console `--next`) open two connections and log in twice, matching curl 8.22.0's recorded exchange; the second, with a wrong password, fails with 67.
- [ ] The same through one `CurlClient` with different credentials fails on the wrong password, pinned by a test.
- [ ] Two requests with the same credentials still reuse one connection, and HTTP reuse is unchanged, pinned by tests.
- [ ] The pool key never holds the password in clear in anything logged or traced.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 2.
