# ADR-0953: A Location host is read as Windows-1252 on Windows, so test1448 stays off the ratchet

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3964 (CGR-059)
- Decided by Claude under Stewart's delegation.

## Context

Upstream test1448 redirects to `Location: http://åäö.se:%HTTPPORT/14480001`, the host
typed as the UTF-8 bytes `C3 A5 C3 A4 C3 B6`, and expects curl to follow it to
`xn--4cab6c.se`. BL-3963 made `CurlUrlHost` read a non-ASCII host as Windows-1252 on
Windows, as the Windows curl 8.22.0 build hands the bytes to `idn2_lookup_ul`; a
`Location` header's bytes reach `CurlUrl` one Latin-1 character per byte, so they take
the same reading.

Measured with `Record-CurlExchange.ps1` against the reference curl 8.22.0
(x86_64-w64-mingw32, Schannel, libidn2 2.3.8) and against Curl, both with `-s -S -i -L`:

| Case | Real curl | Curl |
| --- | --- | --- |
| test1448's command, URL host as the bytes `C3 A5 C3 A4 C3 B6` in the ANSI `argv` | exit 6, `curl: (6) Could not resolve host: xn--bbab6cyjbb.se`, nothing sent | the same |
| URL host typed `åäö` (Windows-1252 `E5 E4 F6`), `--resolve xn--4cab6c.se:...`, Location host as UTF-8 bytes | first request `Host: xn--4cab6c.se:port`, then exit 6 `Could not resolve host: xn--bbab6cyjbb.se`; same stdout and request bytes | the same |
| Location `http://\xFC.se:port/2` (one Windows-1252 byte), `--resolve xn--tda.se:...` | exit 0, second request `Host: xn--tda.se:port` | the same |

So real Windows curl does not pass test1448: its `<setenv>` `LC_ALL=C.UTF-8` does not make
the Windows build read UTF-8 (measured by BL-3963). Off Windows curl reads the host in the
UTF-8 locale and passes it.

## Decision

Change nothing in how a `Location` host is read: it takes the platform's reading, as the
command line's host does. `PassingUpstreamCases.txt` has no per-platform form, and a listed
case must pass on every platform, so test1448 stays off it; on Linux and macOS the ratchet
reports it as an unlisted pass (inconclusive), on Windows as an unlisted failure, each as
the platform's curl answers. `CurlUrlTests` pins both readings, each under its own
`OSCondition`.

## Consequences

Curl matches the Windows curl build byte for byte on a redirect to a UTF-8 host, at the
cost of not following it there. If the ratchet ever gains a per-platform listing, test1448
can be listed for Linux and macOS.

## Whole-host reading across platforms

This ADR also records the reading BL-3963 gave `CurlUrlHost` for a whole non-ASCII host,
which the `Location` decision above follows:

- **Windows:** a host with a byte that is not UTF-8 is read as Windows-1252, the reference
  machine's ANSI code page, because the Windows curl 8.22.0 build hands the bytes to
  `idn2_lookup_ul` in that code page (ADR-0942). A `Location` host of UTF-8 bytes takes the
  same reading, so `åäö` becomes `xn--bbab6cyjbb.se`, not `xn--4cab6c.se`.
- **Linux and macOS:** the answer is the UTF-8 locale's. The same bytes read as UTF-8, so a
  `Location` host of UTF-8 bytes is followed to `xn--4cab6c.se`, as upstream test1448 expects,
  and a byte that is not valid UTF-8 makes the name unconvertible.

The `Location` host answer is not changed because real Windows curl gives the Windows answer
(measured above), and a drop-in replacement matches the platform's curl, not the other
platform's.
