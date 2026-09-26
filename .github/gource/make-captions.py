"""Writes a Gource caption file of Curl's milestones to stdout.

One "timestamp|text" line per milestone, oldest first: every merged pull request (from
the merge commit's subject and title line) and every version tag. Gource shows each
caption as the animation reaches its moment.
"""
import re
import subprocess
import sys

MERGES = ['git', 'log', '--merges', '--exclude=gource', '--branches',
          '--exclude=origin/gource', '--exclude=origin/HEAD', '--remotes',
          '--pretty=format:%at%x1f%s%x1f%b%x1e']
TAGS = ['git', 'for-each-ref', 'refs/tags',
        '--format=%(creatordate:unix)%1f%(refname:short)']


def run(command):
    return subprocess.run(command, check=True, capture_output=True, encoding='utf-8').stdout


def main():
    captions = {}
    for record in run(MERGES).split('\x1e'):
        parts = record.strip('\n').split('\x1f')
        if len(parts) < 3:
            continue
        stamp, subject, body = parts
        pr = re.match(r'Merge pull request #(\d+)', subject)
        if not pr:
            continue
        title = next((line.strip() for line in body.splitlines() if line.strip()), '')
        captions[int(stamp)] = f'Pull request #{pr.group(1)} merged: {title}'.rstrip(': ')
    for line in run(TAGS).splitlines():
        stamp, tag = line.split('\x1f')
        if tag.startswith('v'):
            captions[int(stamp) + 1] = f'Released {tag}'
    for stamp in sorted(captions):
        sys.stdout.write(f'{stamp}|{captions[stamp]}\n')


if __name__ == '__main__':
    main()
