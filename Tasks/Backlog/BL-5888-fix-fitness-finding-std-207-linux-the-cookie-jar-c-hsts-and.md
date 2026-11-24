---
id: BL-5888
title: Fix fitness finding STD-207: Linux: the cookie jar (-c), --hsts and --alt-svc files are rewritten in place, keeping a world-writable mode and writing through a symlink
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary/CookieStore.cs, Curl.Engine.UnitLibrary/HstsCacheFile.cs, Curl.Engine.UnitLibrary/AltSvcTransferCache.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5888 — Fix fitness finding STD-207: Linux: the cookie jar (-c), --hsts and --alt-svc files are rewritten in place, keeping a world-writable mode and writing through a symlink

## Goal

STD-207 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Linux: the cookie jar (-c), --hsts and --alt-svc files are rewritten in place, keeping a world-writable mode and writing through a symlink. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs) · **Area:** state-file writing, Linux build.
- **Mocks:** HTTP on 127.0.0.1:32080 with `/cookie?s=<Set-Cookie value>`; HTTPS on localhost:32443 (self-signed `cert.pem`) whose `/hsts` sends `Strict-Transport-Security`, `Alt-Svc` and the given cookie.
- **Repro** (umask 022):
  - `for f in jar hsts alt; do : > $f.txt; chmod 666 $f.txt; done; curl -s --cacert cert.pem -o /dev/null -c jar.txt --hsts hsts.txt --alt-svc alt.txt "https://localhost:32443/hsts?s=a%3D1"; stat -c '%n %a %i' *.txt`
  - `rm jar.txt; mkdir -p other; ln -s other/victim.txt jar.txt; curl -s -c jar.txt -o /dev/null "http://127.0.0.1:32080/cookie?s=b%3D1"; stat -c %F jar.txt; cat other/victim.txt`
- **Expected (REF2):** all three files get a new inode and mode 0644 (written to a temp file and renamed, with the umask applied). The symlinked `jar.txt` is replaced by a regular file; the link target receives no cookies.
- **Actual (.NET Linux):** same inode, mode stays 0666. The symlink is kept and the full cookie jar (or HSTS cache) is written through it into `other/victim.txt`. `--etag-save` and `-o` behave the same in both clients (written in place).
- **Likely location:** the save paths of the cookie jar (`Curl.Cookies.UnitLibrary`), HSTS and alt-svc stores. Write to a temp file in the same directory and `rename()` it over the target, as libcurl's `Curl_fopen` does.
- **Acceptance check:** after the first command, `stat -c %a jar.txt hsts.txt alt.txt` prints `644` three times with new inodes; after the second, `stat -c %F jar.txt` prints `regular file` and `other/victim.txt` holds no cookie.

## Acceptance criteria

- [ ] after the first command, `stat -c %a jar.txt hsts.txt alt.txt` prints `644` three times with new inodes; after the second, `stat -c %F jar.txt` prints `regular file` and `other/victim.txt` holds no cookie.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
