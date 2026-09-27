# ADR-0024 — Proxy selection follows the platform curl and reads an injected environment

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-206 chooses the `ProxyEndpoint` (ADR-0014) for a URL from `-x`/`--proxy`, `--noproxy`
and the proxy environment variables. Four questions were open: which variable names are
read and in what order, how an exemption list matches, what proxy text curl accepts and
how it refuses the rest, and how the environment reaches the code without a test reading
the real one.

Every answer below was measured with the reference build (curl 8.21.0, mingw, Schannel,
`/mingw64/bin/curl`) on 2026-09-26 by running
`env -u ... <variables> curl -sv --connect-timeout 1 --resolve '*:2222:127.0.0.1' [-x ...] [--noproxy ...] <url>`
and reading from the `Trying` line whether it went to the proxy port or straight to 2222,
or reading the `curl: (N)` line. Proxy credentials were read from the `Proxy-Authorization`
header a loopback listener received. The cases are pinned in `NoProxyMatcherTests`,
`ProxyUrlParserTests` and `ProxySelectorTests` in `Curl.Core.UnitTests`.

## Decision

- **Order.** A `file://` URL never uses a proxy. The exemption list is `--noproxy` when
  given (even empty, which exempts nothing and hides `NO_PROXY`), else `no_proxy`, else
  `NO_PROXY`; it applies to `-x` as well as to the environment. The proxy is `-x` when
  given (`-x ""` is a direct connection), else `<scheme>_proxy`, else `<SCHEME>_PROXY`
  except `HTTP_PROXY`, then `http_proxy` for `ws` and `https_proxy`/`HTTPS_PROXY` for
  `wss`, then `all_proxy`, then `ALL_PROXY`.
- **Names are exact; the reader decides case.** `ProxySelector` takes a
  `Func<string, string?>` and asks for exactly those names. On Windows the process
  environment ignores case, so the measured build uses an upper-case `HTTP_PROXY` as
  `http_proxy`; passing `Environment.GetEnvironmentVariable` reproduces that on Windows and
  curl's case-sensitive behaviour on Linux and macOS, with no platform test in the code.
- **Empty is unset.** An empty variable falls through to the next name, as measured
  (`http_proxy=` with `all_proxy` set uses `all_proxy`). The Linux build's `getenv` returns
  the empty string instead; that difference is not measured here and is left to a later
  task if it matters.
- **Exemption lists** (`NoProxyMatcher`) follow `Curl_check_noproxy`: `*` alone exempts
  everything and is an ordinary entry anywhere else; commas separate entries, a blank not
  followed by a comma ends the list; names match exactly or as a dot-separated suffix,
  ignoring ASCII case, one trailing dot and one leading entry dot; IPv4 and IPv6 hosts
  match only address entries, with an optional all-digit `/bits` where `/0` or none means
  the whole address and too many bits matches nothing.
- **Proxy text** (`ProxyUrlParser`): schemes `http` (and none) on 80, `https` on 443,
  `socks4`, `socks4a`, `socks5`, `socks5h` on 1080, any case; user information split at
  the first `@` and `:` and percent-decoded; path, query and fragment ignored. Syntax
  errors are exit 5 with `Unsupported proxy syntax in '<text>': <reason>` for the six
  reasons measured; another scheme is exit 7, `Unsupported proxy scheme for '<text>'`;
  port 0 is exit 7 with the line curl prints when it tries it.

## Alternatives considered

- **Read `Environment` directly.** Simplest, but every test would then set process-wide
  state, which method-level parallel MSTest cannot share, and the acceptance criteria
  forbid it.
- **Special-case Windows inside the selector** (look up both cases). It would duplicate
  what the operating system already does and be wrong on Linux, where curl reads only the
  exact names.
- **Parse proxy text with `System.Uri`.** It accepts text curl refuses (`a@b@host`,
  `http:////host`), rejects text curl takes (`http:/host`, `host:` after a scheme), and
  cannot give curl's reasons, so the parser is hand-written for the measured cases.

## Consequences

- `Curl.Console` wires `new ProxySelector(Environment.GetEnvironmentVariable)` when it
  builds the transfer; until then nothing calls it.
- The SOCKS options (`--socks4` and friends), `--proxy1.0` and `-U` are not inputs yet;
  combining them with the chosen proxy is the wiring task's job.
- Proxy host names that need IDN conversion, and every URL-parser reason other than the
  six measured, are not reproduced.
