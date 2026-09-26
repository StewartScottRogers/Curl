"""Writes a Gource custom log of every branch's history to stdout.

Gource on its own reads only HEAD. This walks every local and remote branch except
the gource media branch, so feature work shows up before it reaches master, and
prints one "timestamp|author|A/M/D|/path" line per file change, oldest first.
"""
import subprocess
import sys

GIT = [
    'git', 'log',
    '--exclude=gource', '--branches',
    '--exclude=origin/gource', '--exclude=origin/HEAD', '--remotes',
    '--raw', '--no-renames', '--pretty=format:commit|%at|%aN',
]


def main():
    text = subprocess.run(GIT, check=True, capture_output=True, encoding='utf-8').stdout
    rows = []
    stamp, author = None, None
    for line in text.splitlines():
        if line.startswith('commit|'):
            _, stamp, author = line.split('|', 2)
        elif line.startswith(':') and stamp:
            meta, path = line.split('\t', 1)
            status = meta.split()[4][0]
            kind = status if status in 'AD' else 'M'
            rows.append((int(stamp), author, kind, path))
    rows.sort(key=lambda r: r[0])
    out = sys.stdout
    for stamp, author, kind, path in rows:
        out.write(f'{stamp}|{author}|{kind}|/{path}\n')


if __name__ == '__main__':
    main()
