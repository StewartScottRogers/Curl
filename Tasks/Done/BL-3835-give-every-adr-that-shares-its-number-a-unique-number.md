---
id: BL-3835
title: Give every ADR that shares its number a unique number
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-3834]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3835 — Give every ADR that shares its number a unique number

## Goal

Every ADR in `Documentation/Planning/Decisions` has a number no other ADR uses.

## Context

On 2026-10-26, 14 ADR numbers were shared on `work/dark-factory` (ADR-0783, 0785, 0786, 0791, 0793, 0794, 0796, 0801, 0802, 0803, 0804, 0815, 0816, 0817; list them with `git ls-files Documentation/Planning/Decisions | grep -oE 'ADR-[0-9]+' | sort | uniq -d`). Lanes numbered them independently. BL-3834 stops new clashes; this task fixes the existing ones. Waits on BL-3834 so no new clash lands while it runs.

For each shared number, the ADR committed first (by `git log --diff-filter=A`) keeps it; each other one gets the next free number. Rename the file, change its `# ADR-####` heading, and rewrite references to it everywhere (tasks in Backlog, Doing, Blocked and Deferred, docs, code comments, other ADRs), telling apart which of the clashing ADRs each reference means from its title or the commit that added both. Never edit archived tasks under `Tasks/Done/<date>/` (task-board rule); a reference there keeps the old number.

## Acceptance criteria

- [x] `git ls-files Documentation/Planning/Decisions | grep -oE 'ADR-[0-9]+' | sort | uniq -d` prints nothing.
- [x] Every renumbered ADR's heading matches its file name, and Notes list each old and new number with its title.
- [x] `grep` for each moved number outside `Tasks/Done` finds only references to the ADR that kept it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

Renumbered (the ADR committed first keeps its number; 61 ADRs moved, to 0868-0928, 0868 being the next free number at the time). Old number -> new number, title:

- ADR-0783 -> ADR-0868: The Schannel build checks revocation against the system store
- ADR-0785 -> ADR-0869: A library handle starts the low-speed watchdog
- ADR-0785 -> ADR-0870: The differential host runs the built Curl.Console against a loopback server on port 18080
- ADR-0786 -> ADR-0871: The HTTP/2 options keep running on Windows under ADR-0871; CGR-040 is not fixed by refusing them The Windows build runs HTTP/2 and lists HTTP2 in curl -V
- ADR-0791 -> ADR-0872: A Curl.Api request removes a default header through `RemovedHeaders`
- ADR-0791 -> ADR-0873: A SHA Digest challenge fails with exit 94 where the SSPI build is matched
- ADR-0793 -> ADR-0874: The Schannel build ignores `--curves` and `--sigalgs`
- ADR-0793 -> ADR-0875: The Digest problem line follows any answer sent under several allowed schemes
- ADR-0794 -> ADR-0876: The differential runner replays printf scenarios and does not run long double float conversions against the reference
- ADR-0796 -> ADR-0877: CurlUrlHandle.GetBytes gives curl_url_get's part as the C ABI's raw bytes
- ADR-0796 -> ADR-0878: An aborted read stays an abort when the peer's FIN was unread at the write
- ADR-0801 -> ADR-0879: The stdio transport rules of the hand-written MCP server
- ADR-0802 -> ADR-0880: Upstream case 1211 stays off the ratchet: curl 8.22.0 sends `QUIT` and exits 10, as Curl does
- ADR-0802 -> ADR-0881: Upstream case 938 stays off the ratchet: its expected `AUTH PLAIN` authorization identity is not what curl 8.22.0 sends
- ADR-0803 -> ADR-0882: Upstream case 594 stays off the ratchet: libcurl 8.22.0 takes 68 seconds and exits 12, as Curl does
- ADR-0803 -> ADR-0883: The dispatcher rules of the hand-written MCP server
- ADR-0804 -> ADR-0884: The hand-written MCP server's `tools/list` and `tools/call` handlers
- ADR-0804 -> ADR-0885: The resource handlers of the hand-written MCP server
- ADR-0815 -> ADR-0886: With no TLS version option the handshake offers TLS 1.2 and TLS 1.3 only
- ADR-0815 -> ADR-0887: The FTPS data connection's TLS handshake waits for the transfer command
- ADR-0815 -> ADR-0888: A telnet session sends its upload from the receive loop, after each read's replies
- ADR-0815 -> ADR-0889: --connect-timeout holds the SSH handshake and authentication
- ADR-0815 -> ADR-0890: An HTTP/2 or HTTP/3 response header field whose value holds a CR, LF or NUL is ignored
- ADR-0816 -> ADR-0891: An FTP upload's data connection is shut down and drained before it closes
- ADR-0816 -> ADR-0892: LDAP starts TLS or refuses under --ssl and --ssl-reqd, as each platform's curl does
- ADR-0816 -> ADR-0893: `-w` header values keep their raw bytes
- ADR-0816 -> ADR-0894: A redirect target names its scheme with or without `//`
- ADR-0816 -> ADR-0895: A host name that is not ASCII is mapped as UTS #46 does, by hand, before punycode
- ADR-0816 -> ADR-0896: The Schannel build reads `--ciphers` as ALG_IDs
- ADR-0817 -> ADR-0897: Header file bytes reach the wire unchanged, with unreadable bytes escaped as lone surrogates
- ADR-0817 -> ADR-0898: An unfinished header line at close shows the header before it whole
- ADR-0817 -> ADR-0899: A `file://` source that reports size zero is read until its end
- ADR-0817 -> ADR-0900: A refused HTTP/2 stream is retried on a new connection
- ADR-0817 -> ADR-0901: A multi-driven transfer's callbacks run inside perform, on its caller's thread
- ADR-0818 -> ADR-0902: A failed write to a -D, --trace, -c, %output or --stderr file is handled as curl handles it
- ADR-0818 -> ADR-0903: A connect asks its socket opener before each TCP attempt
- ADR-0818 -> ADR-0904: DoH requests go through the proxy environment variables
- ADR-0818 -> ADR-0905: An ASCII FTP download writes LF line endings off Windows
- ADR-0819 -> ADR-0906: An HTTP/2 stream reset with HTTP_1_1_REQUIRED is sent again over HTTP/1.1
- ADR-0820 -> ADR-0907: Gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows
- ADR-0820 -> ADR-0908: The Windows build resolves no IPv4 address written in shorthand
- ADR-0821 -> ADR-0909: Linux and macOS read argv's raw bytes and keep each invalid UTF-8 byte as a surrogate escape
- ADR-0821 -> ADR-0910: A URL's percent-decoded credentials and a netrc file's are sent as their bytes
- ADR-0821 -> ADR-0911: The origin-form path's percent-escapes are sent in upper case
- ADR-0821 -> ADR-0912: `-J` reads the Content-Disposition name in the platform's file-name encoding
- ADR-0821 -> ADR-0913: A timeout past what .NET can arm waits as long as .NET can
- ADR-0821 -> ADR-0914: A socket-action transfer's socket I/O waits for the multi handle's readiness signal, beneath TLS
- ADR-0822 -> ADR-0915: The URL globber takes an IPv6 zone id as curl's URL parser does
- ADR-0822 -> ADR-0916: Off Windows, credentials keep every byte that is not valid UTF-8
- ADR-0822 -> ADR-0917: A socket-action transfer dials through a gated dialer carried on its shared state
- ADR-0822 -> ADR-0918: `--ech` is refused on every platform unless the parse reads as an ECH build
- ADR-0822 -> ADR-0919: A `file` URL's backslashes stay as typed
- ADR-0822 -> ADR-0920: Output file names holding invalid UTF-8 argument bytes open under their raw bytes
- ADR-0823 -> ADR-0921: A URL argument byte that is not valid UTF-8 is sent as itself
- ADR-0824 -> ADR-0922: An unsolicited 101 Switching Protocols is the final head
- ADR-0824 -> ADR-0923: A redirect URL carries an absolute `Location`'s header bytes marked
- ADR-0824 -> ADR-0924: Where the SSPI build is matched, Digest reads and answers a challenge as Windows curl's SSPI does
- ADR-0825 -> ADR-0925: SNI is the URL host lowercased, without a trailing dot
- ADR-0825 -> ADR-0926: `%{certs}` is printed in the layout of the platform's curl build
- ADR-0826 -> ADR-0927: A CONNECT reply fails "Invalid response header" only for a later line without a colon
- ADR-0826 -> ADR-0928: SASL LOGIN sends an empty password as an empty line

- Shared numbers at run time were the 14 listed in Context plus 0818-0826, which clashed after the task was filed; all are fixed.
- Each reference was told apart by title words and file-name links on the same lines, with a hand decision for each that gave no clue. Every ADR-0786 outside the -Z start-order ADR and Curl.Console/ParallelHostQueue.cs means the Windows HTTP/2 ADR (now ADR-0871).
- Left alone as the rules require: archived tasks under Tasks/Done, and the audit and gap offices' folders (off limits to a lane). The audit findings AF-0381, AF-0423 and AF-0432, the audit conformance instructions and the gap finding GF-0099 still say ADR-0786 for the Windows HTTP/2 decision, which is now ADR-0871; an interactive session should update them.
- ADR-0819's file name held adr-0786 in its slug; it is now ...-under-adr-0871-despite-cgr-040 and its links are updated.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d63b0774 b91545c5 caa83799 779790c0.
- 2026-10-28: Doing -> Done. All 61 clashing ADRs renumbered, references rewritten, build clean, fast tests green
