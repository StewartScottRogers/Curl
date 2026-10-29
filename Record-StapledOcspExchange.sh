#!/usr/bin/env bash
# Linux and macOS twin of Record-StapledOcspExchange.ps1 (it also runs in Git Bash and MSYS2 on Windows).
#
# SYNOPSIS
#     Records real curl and Curl against a TLS server that staples a good or a revoked OCSP response (CGR-014).
#
# DESCRIPTION
#     For each side (REF, real curl; PORT, Curl) the script builds a throwaway revocation PKI with
#     openssl in its own temporary directory, so no revocation cache (the Windows URL cache, under
#     Schannel) can carry a result from one side to the other:
#
#       rca         the root certificate authority (the only --cacert)
#       good leaf   CN=localhost, SAN localhost, signed by rca, index.txt entry V
#       revoked     CN=localhost, SAN localhost, signed by rca, index.txt entry R
#       responder   an OCSP signing certificate signed by rca
#       *.ocsp      one DER OCSP response per leaf, signed by the responder
#
#     The leaves carry no CRL distribution point and an Authority Information Access OCSP URL
#     (http://127.0.0.1:9/) that nothing answers, so only the staple can decide revocation.
#     Each leaf is served with `openssl s_server -WWW -status_file <staple>` on --port, and every
#     case runs `curl -sS --cacert <rca.crt> [flags] https://localhost:<port>/index.txt`.
#
#     Cases, per staple (good, revoked): default checking, --ssl-revoke-best-effort, --cert-status,
#     and both flags together. One line per case and side is printed:
#
#       <staple>|<flags>|<side>|exit=<code>|stdout=<text>|stderr=<text>
#
#     with CR, LF written as \r and \n. Everything lives under a temporary directory that is deleted
#     afterwards. CGR-014 compares real curl with Schannel, so the comparison it was written for runs
#     on Windows (Git Bash); on Linux and macOS the reference is the OpenSSL build, which ignores
#     --ssl-revoke-best-effort, and the lines record that build's answers instead.
#     Needs openssl 1.1.1 or later (for req -addext and s_server -status_file). macOS's /usr/bin/openssl
#     is LibreSSL, whose s_server may lack -status_file; the script then stops, saying s_server did not
#     start, and a Homebrew OpenSSL passed with --openssl serves the staple.
#     A missing tool, a missing Curl or a failed openssl step is reported on stderr and exits with status 1.
#
# OPTIONS
#     --port <n>           The loopback TCP port the s_server listens on. Default 14433.
#     --reference <path>   The real curl to run. Default curl from the PATH.
#     --port-curl <path>   The Curl executable to run. Default: the newest Curl.Console/bin/*/*/curl (or curl.exe)
#                          beside this script.
#     --openssl <path>     The openssl used to build the PKI and to serve. Default openssl from the PATH.
#                          (-Port, -Reference, -PortCurl and -OpenSsl are accepted too, as in the PowerShell script.)
#     -h, --help           Print this help and exit.
#
# EXAMPLE
#     ./Record-StapledOcspExchange.sh
#     ./Record-StapledOcspExchange.sh --openssl /opt/homebrew/opt/openssl@3/bin/openssl

set -u
set -o pipefail
# Bash 5.2 treats & in a ${var//pattern/replacement} replacement as the match; 3.2 does not. Keep 3.2's meaning.
shopt -u patsub_replacement 2>/dev/null || true

fail() { echo "Record-StapledOcspExchange.sh: $*" >&2; exit 1; }

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 1
port=14433
reference=curl
port_curl=""
openssl=openssl
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help) sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        --port|-Port|--reference|-Reference|--port-curl|-PortCurl|--openssl|-OpenSsl)
            [ $# -ge 2 ] || fail "$1 needs a value."
            case "$1" in
                --port|-Port) port="$2" ;;
                --reference|-Reference) reference="$2" ;;
                --port-curl|-PortCurl) port_curl="$2" ;;
                --openssl|-OpenSsl) openssl="$2" ;;
            esac
            shift 2 ;;
        *) fail "unknown option '$1'. Use --port, --reference, --port-curl or --openssl." ;;
    esac
done
case "$port" in ''|*[!0-9]*) fail "--port must be a number, not '$port'." ;; esac

for tool in "$openssl" "$reference" date mktemp sleep; do
    command -v "$tool" >/dev/null 2>&1 || fail "needs '$tool' (not found on PATH or not executable)."
done

if [ -z "$port_curl" ]; then
    # The newest curl or curl.exe anywhere under Curl.Console/bin.
    while IFS= read -r candidate; do
        if [ -z "$port_curl" ] || [ "$candidate" -nt "$port_curl" ]; then port_curl="$candidate"; fi
    done <<EOF
$(find "$script_dir/Curl.Console/bin" -type f \( -name curl -o -name curl.exe \) 2>/dev/null)
EOF
fi
[ -n "$port_curl" ] || fail 'No Curl executable found: build Curl.Console or pass --port-curl.'
command -v "$port_curl" >/dev/null 2>&1 || fail "the Curl executable '$port_curl' does not exist or is not executable."

# Runs openssl in a directory and stays quiet unless it fails, when its output goes into the error.
# Every argument is relative to that directory, so Git Bash and MSYS2 path conversion (which would turn
# -subj /CN=rca into a Windows path) is switched off.
invoke_openssl() {
    local directory="$1" output
    shift
    output="$(cd "$directory" && MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' "$openssl" "$@" 2>&1)" ||
        fail "openssl $* failed: $output"
}

# UTC time N days from now (N may be negative) as an OCSP index file holds it (yyMMddHHmmssZ).
# GNU date takes -d @N, BSD date (macOS) takes -r N.
utc_time_days_from_now() {
    local epoch=$(($(date -u +%s) + $1 * 86400))
    date -u -d "@$epoch" '+%y%m%d%H%M%SZ' 2>/dev/null || date -u -r "$epoch" '+%y%m%d%H%M%SZ'
}

new_revocation_pki() {
    local directory="$1" serial=16 name extension subject hex_serial expiry revoked index_lines=""
    mkdir -p "$directory" || fail "cannot create '$directory'."
    printf 'basicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nsubjectAltName=DNS:localhost\nauthorityInfoAccess=OCSP;URI:http://127.0.0.1:9/\n' > "$directory/leaf.ext"
    printf 'basicConstraints=CA:FALSE\nextendedKeyUsage=OCSPSigning\n' > "$directory/resp.ext"
    invoke_openssl "$directory" req -x509 -newkey rsa:2048 -nodes -keyout rca.key -out rca.crt -days 30 -subj /CN=rca \
        -addext basicConstraints=critical,CA:TRUE -addext keyUsage=critical,keyCertSign,cRLSign
    for name in good revoked resp; do
        serial=$((serial + 1))
        if [ "$name" = resp ]; then extension=resp.ext; subject=/CN=ocsp-responder; else extension=leaf.ext; subject=/CN=localhost; fi
        invoke_openssl "$directory" req -newkey rsa:2048 -nodes -keyout "$name.key" -out "$name.csr" -subj "$subject"
        invoke_openssl "$directory" x509 -req -in "$name.csr" -CA rca.crt -CAkey rca.key -set_serial "$serial" -out "$name.crt" -days 30 -extfile "$extension"
        hex_serial="$(printf '%02X' "$serial")"
        expiry="$(utc_time_days_from_now 30)"
        revoked="$(utc_time_days_from_now -1)"
        if [ "$name" = good ]; then index_lines="${index_lines}V	$expiry		$hex_serial	unknown	/CN=localhost
"; fi
        if [ "$name" = revoked ]; then index_lines="${index_lines}R	$expiry	$revoked	$hex_serial	unknown	/CN=localhost
"; fi
    done
    printf '%s' "$index_lines" > "$directory/ocsp-index.txt"
    for name in good revoked; do
        invoke_openssl "$directory" ocsp -index ocsp-index.txt -CA rca.crt -rsigner resp.crt -rkey resp.key -issuer rca.crt -cert "$name.crt" -ndays 7 -respout "$name.ocsp"
    done
    printf '%s' 'stapled ocsp ok' > "$directory/index.txt"
}

# Prints a file's bytes on one line, CR and LF written as \r and \n (the x keeps trailing newlines).
convert_to_one_line() {
    local text cr='\r' lf='\n'
    text="$(cat "$1"; printf x)"
    text="${text%x}"
    text="${text//$'\r'/$cr}"
    text="${text//$'\n'/$lf}"
    printf '%s' "$text"
}

server_pid=""
stop_server() {
    if [ -n "$server_pid" ]; then
        kill "$server_pid" 2>/dev/null
        wait "$server_pid" 2>/dev/null
        server_pid=""
    fi
}

invoke_side() {
    local side="$1" executable="$2" directory="$3" staple flags code
    for staple in good revoked; do
        (cd "$directory" && MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' exec "$openssl" s_server -accept "$port" \
            -cert "$staple.crt" -key "$staple.key" -status_file "$staple.ocsp" -WWW) </dev/null >"$root/s_server.log" 2>&1 &
        server_pid=$!
        sleep 1.5
        # A server that could not start (port taken, no -status_file) would leave every case talking to nothing.
        kill -0 "$server_pid" 2>/dev/null || fail "openssl s_server did not start on port $port: $(cat "$root/s_server.log")"
        for flags in '' '--ssl-revoke-best-effort' '--cert-status' '--ssl-revoke-best-effort --cert-status'; do
            # $flags is split on purpose: it holds zero, one or two options with no spaces in them.
            # shellcheck disable=SC2086
            "$executable" -sS --cacert "$directory/rca.crt" $flags "https://localhost:$port/index.txt" >"$root/stdout" 2>"$root/stderr"
            code=$?
            echo "$staple|$flags|$side|exit=$code|stdout=$(convert_to_one_line "$root/stdout")|stderr=$(convert_to_one_line "$root/stderr")"
        done
        stop_server
    done
}

root="$(mktemp -d "${TMPDIR:-/tmp}/stapled-ocsp-XXXXXXXX")" || fail "cannot create a temporary directory."
trap 'stop_server; rm -rf "$root"' EXIT
for side in REF PORT; do
    if [ "$side" = REF ]; then executable="$reference"; else executable="$port_curl"; fi
    new_revocation_pki "$root/$side"
    invoke_side "$side" "$executable" "$root/$side"
done
