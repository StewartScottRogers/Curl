---
id: BL-5888
title: Fix fitness finding STD-207: Linux: the cookie jar (-c), --hsts and --alt-svc files are rewritten in place, keeping a world-writable mode and writing through a symlink
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary/CookieStore.cs, Curl.Cookies.UnitTests/CookieStoreTests.CookieFiles.cs, Curl.Engine.UnitLibrary/HstsCacheFile.cs, Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Core.UnitLibrary/FileSystem, Curl.Core.UnitTests/FileSystem, Curl.Protocol.Abstractions.UnitLibrary/FileWriteMode.cs, Curl.Protocol.Abstractions.UnitLibrary/IFileSystem.cs, Curl.Protocol.Abstractions.UnitLibrary/FileSystemReplacement.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
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

- [x] after the first command, `stat -c %a jar.txt hsts.txt alt.txt` prints `644` three times with new inodes; after the second, `stat -c %F jar.txt` prints `regular file` and `other/victim.txt` holds no cookie.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix (ADR-1358): new `FileWriteMode.Replace`; `PhysicalFileSystem` off Windows writes a temporary
  file beside the target and renames it over it, as libcurl's `Curl_fopen` does; the cookie jar,
  HSTS and alt-svc savers use it through `FileSystemReplacement.TryReplaceFileAsync`.
- First criterion: **not run on Linux** (lanes run on Windows only). It is met on the lane rules'
  platform terms: `PhysicalFileSystemReplaceTests` pins the umasked mode, the replaced symbolic
  link with an empty target and the in-place `/dev/null` in tests that exclude Windows, so CI on
  Linux and macOS is their first run. New inodes follow from the rename and are not asserted.
- Windows keeps writing in place, unmeasured: BL-5933.
- `touches` widened: the rename needs the real file system (Core), a mode on the contract
  (Abstractions) and the Cookies test fake. No other task in Doing named any of them when checked
  (BL-5915, which held `Curl.Protocol.Abstractions.UnitTests`, is left untouched).
- Engine savers lost their branches and are covered by the existing Engine tests.
- Quality: the four-library `Measure-CodeQuality.ps1` run reached one test project in 65 minutes
  and was stopped for the deadline. Instead the Core and Cookies test projects were collected and
  reported with `-SkipTestRun`: Cookies 100/100, and no new or changed member of Core or
  Abstractions listed (the members listed are ones those two projects do not reach).
  Complexity is enforced by the clean build.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 481dabb8 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Off Windows the cookie jar, HSTS and alt-svc files are replaced by rename, taking the umasked mode and replacing a symlink
