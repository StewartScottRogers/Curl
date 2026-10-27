#!/usr/bin/env bash
# Linux and macOS twin of New-DifferentialPki.ps1 (it also runs in Git Bash and MSYS2 on Windows).
#
# SYNOPSIS
#     Generates the certificate directory the TLS differential cases name as {PKI}, with no interaction.
#
# DESCRIPTION
#     Writes these files into --directory (created if missing; existing files are overwritten, so running
#     it twice is safe). Every certificate is signed by the generated ca.pem, which is the only
#     certificate authority; private keys are written only inside --directory (the CA's key lives in a
#     scratch folder there for the run and is deleted with it). Uses only the openssl command line tool
#     (OpenSSL 1.1 or 3, or LibreSSL as macOS's /usr/bin/openssl ships it) and date.
#
#       ca.pem                  The certificate authority (ECDSA P-256). The cases pass it as --cacert and
#                               the MTLS endpoint accepts only client certificates that chain to it.
#       server.pem, server.key  Server certificate (ECDSA) for 127.0.0.1, ::1 and localhost: served by
#                               the T, T12, T13 and MTLS endpoints. Also used as a wrong --cacert.
#       wrong.pem,  wrong.key   Server certificate for another host name (wrong.example): the WRONG endpoint.
#       expired.pem, expired.key Server certificate for 127.0.0.1 and localhost that expired a year ago: the EXP endpoint.
#       rsa.pem,    rsa.key     Server certificate with an RSA 2048 key, otherwise like server: the RSA endpoint.
#       client.pem, client.key  Client certificate (ECDSA) for the MTLS endpoint, PEM certificate and PKCS#8 key.
#       client.p12              The same client certificate and key as PKCS#12 with the password "pw".
#
#     The certificates carry the same subjects, extensions, validity and serial size as the PowerShell
#     script's, and client.p12 uses the same protection (3DES PBE for key and certificates, SHA-1 MAC,
#     2000 iterations) so every TLS stack, OpenSSL 3 without its legacy provider included, opens it.
#     Every failure is reported on stderr and exits with status 1.
#
# OPTIONS
#     --directory <path>   The directory to write into. Point CURL_DIFFERENTIAL_PKI at it.
#                          (-Directory, or the path alone, is accepted too, as in the PowerShell script.)
#     -h, --help           Print this help and exit.
#
# EXAMPLE
#     ./New-DifferentialPki.sh --directory "${TMPDIR:-/tmp}/curl-pki"

set -u
set -o pipefail

fail() { echo "New-DifferentialPki.sh: $*" >&2; exit 1; }

directory=""
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help) sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        --directory|-Directory)
            [ $# -ge 2 ] || fail "$1 needs a value."
            directory="$2"; shift 2 ;;
        -*) fail "unknown option '$1'. Use --directory <path>." ;;
        *)
            [ -z "$directory" ] || fail "unexpected argument '$1'; only one directory is taken."
            directory="$1"; shift ;;
    esac
done
[ -n "$directory" ] || fail "--directory <path> is required."

for tool in openssl date; do
    command -v "$tool" >/dev/null 2>&1 || fail "needs '$tool' on PATH."
done

mkdir -p "$directory" || fail "cannot create '$directory'."
directory="$(cd "$directory" && pwd)" || fail "cannot resolve '$directory'."

# Scratch folder for the CA key, the requests and openssl ca's database. It sits inside the directory so
# no private key is ever written anywhere else, and it is deleted however the script ends.
work="$(mktemp -d "$directory/.new-differential-pki.XXXXXX")" || fail "cannot create a scratch folder in '$directory'."
trap 'cd / && rm -rf "$work"' EXIT
# Every openssl call runs here with relative file names, so the config files hold no absolute path
# (a native Windows openssl under Git Bash cannot open a /c/... path written inside a file).
cd "$work" || fail "cannot enter '$work'."

# Runs openssl and stays quiet unless it fails, when its output goes into the error. Git Bash and MSYS2
# would rewrite -subj /CN=... into a Windows path; every argument here is relative, so conversion is off.
invoke_openssl() {
    local output
    output="$(MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' openssl "$@" 2>&1)" || fail "openssl $* failed: $output"
}

# UTC time as openssl ca's -startdate and -enddate take it (yyMMddHHmmssZ), for an epoch second count.
# GNU date takes -d @N, BSD date (macOS) takes -r N.
utc_time_of_epoch() {
    date -u -d "@$1" '+%y%m%d%H%M%SZ' 2>/dev/null || date -u -r "$1" '+%y%m%d%H%M%SZ'
}

# UTC time N calendar years from now (N may be negative), like DateTimeOffset.AddYears: 29 February
# becomes 28 February in a year that has none.
utc_time_years_from_now() {
    local year=$((now_year + $1)) rest="$now_rest"
    if [ "${rest:0:4}" = 0229 ] && { [ $((year % 4)) -ne 0 ] || { [ $((year % 100)) -eq 0 ] && [ $((year % 400)) -ne 0 ]; }; }; then
        rest="0228${rest:4}"
    fi
    echo "${year:2:2}${rest}Z"
}

# An 8-byte random serial with the top bit clear, so it is positive, as the PowerShell script draws it.
new_serial() {
    local hex
    hex="$(openssl rand -hex 8)" || fail "openssl rand failed."
    printf '%X%s\n' $((0x${hex:0:1} & 7)) "${hex:1}"
}

# A fresh P-256 or RSA 2048 key, written unencrypted as PKCS#8 ("BEGIN PRIVATE KEY") like the CNG export.
new_key() {
    local kind="$1" path="$2"
    if [ "$kind" = rsa ]; then
        invoke_openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out key.tmp
    else
        invoke_openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -pkeyopt ec_param_enc:named_curve -out key.tmp
    fi
    invoke_openssl pkcs8 -topk8 -nocrypt -in key.tmp -out "$path"
    rm -f key.tmp
}

now="$(date -u +%s)" || fail "date failed."
stamp="$(date -u -d "@$now" '+%Y%m%d%H%M%S' 2>/dev/null || date -u -r "$now" '+%Y%m%d%H%M%S')" || fail "date failed."
now_year="${stamp:0:4}"
now_rest="${stamp:4}"

# openssl ca's state: an empty database that allows the same subject twice (server, expired and rsa are
# all CN=localhost), a serial file rewritten before each certificate, and the config, all relative to here.
mkdir new
: > index.txt
echo 'unique_subject = no' > index.txt.attr
cat > openssl.cnf <<'EOF'
[ ca ]
default_ca = differential_ca

[ differential_ca ]
database = index.txt
serial = serial
new_certs_dir = new
certificate = ../ca.pem
private_key = ca.key
default_md = sha256
policy = policy_any
unique_subject = no
email_in_dn = no
copy_extensions = none

[ policy_any ]
commonName = supplied

[ req ]
distinguished_name = req_distinguished_name

[ req_distinguished_name ]
commonName = Common Name
EOF

# OpenSSL 3 adds subject and authority key identifiers to every certificate unless told "none"; the .NET
# CertificateRequest adds neither. OpenSSL 1.1 and LibreSSL add none by themselves and reject "none".
no_key_identifiers=""
case "$(openssl version 2>/dev/null)" in
    OpenSSL\ [3-9]*) no_key_identifiers='subjectKeyIdentifier = none
authorityKeyIdentifier = none' ;;
esac

# Signs a request with openssl ca, between two yyMMddHHmmssZ times, adding only the extensions written
# for it.
sign_request() {
    local request="$1" certificate="$2" not_before="$3" not_after="$4"
    shift 4
    new_serial > serial
    invoke_openssl ca -batch -config openssl.cnf -notext -md sha256 -startdate "$not_before" -enddate "$not_after" \
        -extfile extensions.cnf -extensions certificate_extensions -in "$request" -out "$certificate" "$@"
}

# The CA: basic constraints critical CA:TRUE with no path length and nothing else, valid from five years
# ago to ten years ahead, signing itself.
new_key ec ca.key
printf '[ certificate_extensions ]\nbasicConstraints = critical,CA:TRUE\n%s\n' "$no_key_identifiers" > extensions.cnf
invoke_openssl req -new -config openssl.cnf -key ca.key -subj '/CN=Curl differential test CA' -out ca.csr
sign_request ca.csr ../ca.pem "$(utc_time_years_from_now -5)" "$(utc_time_years_from_now 10)" -selfsign -keyfile ca.key

# Issues <name>.pem and <name>.key: CN is the first name, the subject alternative names are all of them
# (IP addresses as IP entries, the rest as DNS entries, in order), with basic constraints critical
# CA:FALSE, key usage critical digitalSignature and extended key usage serverAuth and clientAuth.
new_issued() {
    local name="$1" kind="$2" not_before="$3" not_after="$4" alt_names="" host
    shift 4
    for host in "$@"; do
        case "$host" in
            *:*) alt_names="$alt_names,IP:$host" ;;
            *[!0-9.]*) alt_names="$alt_names,DNS:$host" ;;
            *) alt_names="$alt_names,IP:$host" ;;
        esac
    done
    new_key "$kind" "../$name.key"
    {
        echo '[ certificate_extensions ]'
        echo 'basicConstraints = critical,CA:FALSE'
        echo 'keyUsage = critical,digitalSignature'
        echo 'extendedKeyUsage = serverAuth,clientAuth'
        echo "subjectAltName = ${alt_names#,}"
        echo "$no_key_identifiers"
    } > extensions.cnf
    invoke_openssl req -new -config openssl.cnf -key "../$name.key" -subj "/CN=$1" -out "$name.csr"
    sign_request "$name.csr" "../$name.pem" "$not_before" "$not_after"
}

valid_from="$(utc_time_of_epoch $((now - 86400)))" || fail "date failed."
valid_to="$(utc_time_years_from_now 5)"
new_issued server ec "$valid_from" "$valid_to" localhost 127.0.0.1 ::1
new_issued wrong ec "$valid_from" "$valid_to" wrong.example
new_issued expired ec "$(utc_time_years_from_now -2)" "$(utc_time_years_from_now -1)" localhost 127.0.0.1 ::1
new_issued rsa rsa "$valid_from" "$valid_to" localhost 127.0.0.1 ::1
new_issued client ec "$valid_from" "$valid_to" curl-differential-client

invoke_openssl pkcs12 -export -in ../client.pem -inkey ../client.key -out ../client.p12 -passout pass:pw \
    -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1 -iter 2000

# A native Windows openssl (Git Bash) writes PEM with CRLF; write LF everywhere, as the PowerShell script does.
for name in ca server wrong expired rsa client; do
    for file in "../$name.pem" "../$name.key"; do
        [ -f "$file" ] || continue
        tr -d '\r' < "$file" > lf.tmp && cat lf.tmp > "$file" || fail "cannot rewrite '$file' with LF line ends."
    done
done
echo "Wrote the differential PKI to $directory"
