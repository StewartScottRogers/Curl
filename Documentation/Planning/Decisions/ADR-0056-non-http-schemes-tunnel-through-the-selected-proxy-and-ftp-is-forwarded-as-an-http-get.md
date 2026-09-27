# ADR-0056 — Non-HTTP schemes tunnel through the selected proxy, and `ftp://` is forwarded as an HTTP GET

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; recorded
  by BL-330, 2026-09-27).

## Context

BL-238 wired proxy selection into `Curl.Console` (ADR-0024, ADR-0053), but the proxy it picks
reaches only `HttpRequestOptions.ForwardProxy`, which only `HttpProtocolHandler` reads. Every
other handler builds its own `ConnectTarget` without a `Proxy`, so `curl -x h dict://x/`,
`all_proxy` or `--socks5` connect straight to the origin. That silently bypasses a proxy the
user named, and it is not what curl does.

BL-330 measured curl 8.21.0 (the mingw Schannel build, ADR-0009, ADR-0018) against a loopback
listener standing in for an HTTP proxy. The exact commands and bytes are in BL-330's `Notes`.
In short:

| Command | What the proxy received | Result |
| --- | --- | --- |
| `curl -x http://p dict://example.com/d:x` | `CONNECT example.com:2628 HTTP/1.1`, `Host: example.com:2628`, `User-Agent: curl/8.21.0`, `Proxy-Connection: Keep-Alive`; after `200`, the DICT commands `CLIENT libcurl 8.21.0` / `DEFINE ! x` / `QUIT` | tunnelled |
| `curl -p -x http://p dict://example.com/d:x` | the same bytes | tunnelled |
| `curl -x http://p gopher://example.com/` | `CONNECT example.com:70 …`; after `200`, the selector `\r\n` | tunnelled |
| `curl -x http://p ftp://example.com/f.txt` | `GET ftp://example.com/f.txt HTTP/1.1`, `Host: example.com:21`, `User-Agent`, `Accept: */*`, `Proxy-Connection: Keep-Alive` | forwarded; the proxy's HTTP body is the output |
| `curl -p -x http://p ftp://example.com/f.txt` | `CONNECT example.com:21 …` | tunnelled |
| `telnet`, `imap`, `pop3`, `smtp`, `mqtt`, `rtsp`, `ws`, `ldap` | `CONNECT <host>:<default port> …` | tunnelled |
| `curl -x http://p tftp://example.com/f` | `GET http://p/.well-known/masque/udp/example.com/69/` with `Upgrade: connect-udp` | exit 7 `bind() failed; Invalid arguments` |
| `curl -x http://p file:///…` | nothing; no connection | proxy ignored |
| CONNECT answered `403` (dict) | — | exit 7 `CONNECT tunnel failed, response 403` |

This is libcurl's rule: a scheme whose handler does not set `PROTOPT_PROXY_AS_HTTP` is always
tunnelled through an HTTP proxy, `-p` or not; `ftp` is the one non-HTTP scheme libcurl hands
to its HTTP code when not tunnelling; `file` never uses a proxy.

## Decision

1. **The transfer context carries the proxy.** `ITransferContext` gains a scheme-neutral
   `Proxy` (`ProxyEndpoint?`), set by `Curl.Console` from `TransferProxySelection` for every
   scheme except `file`, with `-U` applied. `HttpRequestOptions.ForwardProxy` stays the HTTP
   handler's input for now; both are set from the one selection, so they cannot disagree.
2. **TCP schemes tunnel.** Every handler that connects over TCP (`dict`, `gopher`, `telnet`,
   `mqtt`, and `imap`, `pop3`, `smtp`, `rtsp`, `ws`, `ldap`, `scp`/`sftp`, `smb` as they are
   built) sets `ConnectTarget.Proxy = context.Proxy` for its control connection. The connector
   already writes curl's CONNECT bytes (ADR-0023) and reports a refused tunnel, so the handler
   adds no proxy code of its own. `-p` changes nothing for these schemes.
3. **`ftp://` without `-p` through an HTTP proxy is forwarded as an HTTP GET.** `Curl.Console`
   routes it to `HttpProtocolHandler`, which writes the measured request (absolute `ftp://`
   URI, `Host` with `:21`) and treats the reply as an HTTP response. With `-p`, or through a
   SOCKS proxy, `ftp` tunnels like rule 2 once the FTP handler exists.
4. **`tftp://` through an HTTP proxy fails as the reference build fails:** the MASQUE
   `connect-udp` request is sent to the proxy, then exit 7 with `bind() failed; Invalid
   arguments`. It is a Windows build quirk, but it is what the platform's curl does, and it
   never sends TFTP traffic around the proxy.
5. **`file://` ignores the proxy**, as now.
6. **The ADR-0053 guard widens.** A tunnelled non-HTTP scheme through a SOCKS or HTTPS proxy
   is refused with exit 4 before connecting, exactly as `http`/`https` are, until BL-213 and
   BL-266 land (BL-328 removes the guard).

## Consequences

- No handler can bypass a named proxy; a scheme either tunnels, is forwarded, or fails.
- Handlers stay free of proxy logic: one `with { Proxy = … }` on the target is the whole change
  for rule 2, and each is tested with a fake connector that asserts the target's `Proxy`.
- Two members hold the same proxy until HTTP moves to `ITransferContext.Proxy`; that move is
  a later cleanup, not part of this decision.
- The `tftp` failure text is measured Schannel-build text; a Linux build would differ.

## Alternatives considered

- **Keep connecting directly for non-HTTP schemes.** Lost: it leaks traffic the user routed
  to a proxy and differs from curl on the bytes on the wire.
- **Put the proxy on each protocol's own options.** Lost: one concept would have a dozen
  names; the context member is the one seam every handler already receives.
- **Tunnel only under `-p`.** Lost: measured curl tunnels `dict` and `gopher` without `-p`.
- **Tunnel `ftp` without `-p` too.** Lost: measured curl forwards it as an HTTP GET, and a
  proxy that only forwards would reject the CONNECT.
- **Refuse `tftp` through a proxy with Curl's own message.** Lost: the reference build's
  exit code and text are measurable, so they are matched.
