#!/usr/bin/env bash
# Linux and macOS twin of Update-PublicSuffixList.ps1.
#
# SYNOPSIS
#     Refreshes the Public Suffix List snapshot embedded in Curl.Cookies.UnitLibrary.
#
# DESCRIPTION
#     Downloads https://publicsuffix.org/list/public_suffix_list.dat (with curl, else wget) and writes it to
#     Curl.Cookies.UnitLibrary/PublicSuffixList/public_suffix_list.dat as ADR-0049 says:
#     one dated comment line first,
#
#       // Curl snapshot: yyyy-MM-dd, from https://publicsuffix.org/list/public_suffix_list.dat
#
#     ended by a line feed, then the upstream bytes unchanged. The upstream MPL-2.0 notice
#     is part of those bytes and so is kept. The README beside the snapshot is left alone.
#     The file is only replaced once the whole download has been checked, so a failure changes nothing.
#
#     The build never runs this script; a task runs it when a refresh is wanted and commits
#     the result. The date is today's date (UTC) unless --snapshot-date is given.
#     Every failure is reported on stderr and exits with status 1.
#
# OPTIONS
#     --snapshot-date <date>   The date written into the first line, as yyyy-MM-dd. Defaults to today (UTC).
#                              (-SnapshotDate is accepted too, as in the PowerShell script.)
#     --output <path>          Where to write the snapshot; default is the path above, beside this script.
#                              Not in the PowerShell script; it lets the result be inspected without touching the repository.
#     -h, --help               Print this help and exit.
#
# EXAMPLE
#     ./Update-PublicSuffixList.sh
#     ./Update-PublicSuffixList.sh --snapshot-date 2026-10-27

set -u
set -o pipefail

fail() { echo "Update-PublicSuffixList.sh: $*" >&2; exit 1; }

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 1
source_uri='https://publicsuffix.org/list/public_suffix_list.dat'
snapshot_path="$script_dir/Curl.Cookies.UnitLibrary/PublicSuffixList/public_suffix_list.dat"
snapshot_date="$(date -u +%Y-%m-%d)"

while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help) sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        --snapshot-date|-SnapshotDate|--output)
            [ $# -ge 2 ] || fail "$1 needs a value."
            if [ "$1" = "--output" ]; then snapshot_path="$2"; else snapshot_date="$2"; fi
            shift 2 ;;
        *) fail "unknown option '$1'. Use --snapshot-date or --output." ;;
    esac
done

# The date must be a real yyyy-MM-dd calendar date (leap years included), as DateTime.TryParseExact requires.
case "$snapshot_date" in
    [0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]) ;;
    *) fail "SnapshotDate must be yyyy-MM-dd, not '$snapshot_date'." ;;
esac
year=$((10#${snapshot_date%%-*}))
rest="${snapshot_date#*-}"
month=$((10#${rest%%-*}))
day=$((10#${rest#*-}))
case "$month" in
    1|3|5|7|8|10|12) days_in_month=31 ;;
    4|6|9|11) days_in_month=30 ;;
    2) if [ $((year % 4)) -eq 0 ] && { [ $((year % 100)) -ne 0 ] || [ $((year % 400)) -eq 0 ]; }; then days_in_month=29; else days_in_month=28; fi ;;
    *) days_in_month=0 ;;
esac
if [ "$year" -lt 1 ] || [ "$day" -lt 1 ] || [ "$day" -gt "$days_in_month" ]; then
    fail "SnapshotDate must be yyyy-MM-dd, not '$snapshot_date'."
fi

download_path="$(mktemp "${TMPDIR:-/tmp}/public_suffix_list.XXXXXX")" || fail "cannot create a temporary file."
output_temp=""
trap 'rm -f "$download_path" "$output_temp"' EXIT

# Download the upstream bytes as they are (TLS 1.2 or newer, no redirect to anything but https).
if command -v curl >/dev/null 2>&1; then
    curl --fail --silent --show-error --location --proto '=https' --proto-redir '=https' --tlsv1.2 --output "$download_path" "$source_uri" \
        || fail "The download from $source_uri failed; the snapshot was not changed."
elif command -v wget >/dev/null 2>&1; then
    wget --quiet --secure-protocol=TLSv1_2 --output-document="$download_path" "$source_uri" \
        || fail "The download from $source_uri failed; the snapshot was not changed."
else
    fail "Neither curl nor wget was found to download $source_uri."
fi

if ! grep -aqF '// ===BEGIN ICANN DOMAINS===' "$download_path" || ! grep -aqF '// ===BEGIN PRIVATE DOMAINS===' "$download_path"; then
    fail "The download from $source_uri does not look like the Public Suffix List; the snapshot was not changed."
fi

snapshot_directory="$(dirname "$snapshot_path")"
mkdir -p "$snapshot_directory" || fail "cannot create '$snapshot_directory'."

# The dated line, a line feed, then the upstream bytes unchanged.
output_temp="$snapshot_path.tmp.$$"
{ printf '// Curl snapshot: %s, from %s\n' "$snapshot_date" "$source_uri"; cat "$download_path"; } > "$output_temp" \
    || fail "cannot write '$output_temp'."
mv -f "$output_temp" "$snapshot_path" || fail "cannot replace '$snapshot_path'."
output_temp=""

byte_count="$(wc -c < "$snapshot_path" | tr -d '[:space:]')"
echo "Wrote $byte_count bytes to $snapshot_path (snapshot $snapshot_date)."
