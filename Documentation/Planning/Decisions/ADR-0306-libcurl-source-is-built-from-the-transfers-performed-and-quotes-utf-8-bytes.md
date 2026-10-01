# ADR-0306 — `--libcurl` source is built from the transfers performed and quotes UTF-8 bytes

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-652.

## Context

curl 8.21.0's `--libcurl <file>` records every `curl_easy_setopt` its tool makes and writes them as
a C program once the transfers are done. Curl has no libcurl underneath, so the lines have to be
generated from the command line. BL-652 writes the skeleton; BL-653 and BL-654 add the lines other
options produce. Measured on Windows (mingw, Schannel) on 2026-10-01 (BL-652 Notes).

## Decision

1. **`Curl.Cli.UnitLibrary`'s `LibcurlSourceCode.Generate` takes the transfers performed**, each as
   its option group and its URL as the glob expanded it, and `CurlCommandRunner` records them as
   each transfer starts (after an IPFS rewrite, before a scheme is guessed). A glob or several URLs
   therefore give one block per transfer, as curl's do.
2. **Scheme lines follow the URL's scheme** as curl's tool finds it: its own, `--proto-default`'s,
   or `UrlSchemeGuesser`'s guess. `http`/`https` add `CURLOPT_MAXREDIRS`, `ftp`/`ftps` add
   `CURLOPT_FTP_SKIP_PASV_IP`. A URL holding a space or control character is taken as one curl's
   URL parser refuses, so it gets neither. Other parse failures are not modelled.
3. **Strings are quoted as UTF-8 bytes**, cut at 2000 bytes with `...` added, with curl's `\n \r \t
   \\ \" \?` escapes and `\xHH` for any other byte outside printable ASCII. curl writes the bytes
   its `argv` held. On Linux and macOS those are UTF-8. On Windows curl 8.21.0 showed `é` as
   `\xe9`, the ANSI code page. Curl gets its arguments as UTF-16 everywhere and uses UTF-8 as the
   single answer.
4. **Line ends:** `-` writes `\n` to standard output after the transfers' own output, and a file is
   written in text mode, so `\r\n` on Windows and `\n` elsewhere. A file that cannot be opened gets
   `Warning: Failed to open <file> to write libcurl code`, unless `-s` was given. The source is
   written whether the transfers succeeded or not.
5. **SSH setup failures stay out of scope.** curl cuts its source short when an `scp`/`sftp`
   transfer fails during setup, for example with no `known_hosts`. That case is left to BL-654.

## Alternatives considered

- **Generate from the option groups' URL lists alone.** This was rejected because it would miss glob
  expansion and the IPFS gateway rewrite, so it would no longer match curl for `[1-2]` URLs.
- **Quote Windows arguments in the ANSI code page.** This was rejected because it makes the output
  depend on the machine's code page, and the BCL gives no portable way to read the code page curl
  would have used.
