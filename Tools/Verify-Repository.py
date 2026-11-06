"""Check the publishable source tree; optionally resolve registry asset GUIDs from a package cache."""
from pathlib import Path
import argparse
import json
import re
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--package-cache', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
guid_re = re.compile(r'guid:\s*([0-9a-fA-F]{32})')
assets = root/'Assets'
known = set()
for meta in assets.rglob('*.meta'):
    match = guid_re.search(meta.read_text(encoding='utf-8', errors='ignore'))
    if match:
        if match[1] in known:
            raise SystemExit('Duplicate asset GUID: '+meta.relative_to(root).as_posix())
        known.add(match[1])
if args.package_cache:
    text = subprocess.check_output(['rg','--no-ignore','--no-heading','-g','*.meta','^guid: [a-fA-F0-9]{32}',str(args.package_cache)],text=True)
    known.update(guid_re.findall(text))
failures = []
for path in assets.rglob('*'):
    if path.is_file() and path.suffix != '.meta' and not Path(str(path)+'.meta').exists():
        failures.append('Missing Unity .meta file: '+path.relative_to(root).as_posix())
for path in list(assets.rglob('*'))+list((root/'ProjectSettings').glob('*')):
    if path.is_file() and path.suffix in {'.asset','.unity','.prefab','.mat','.shadergraph','.shadersubgraph'} and args.package_cache:
        for guid in guid_re.findall(path.read_text(encoding='utf-8',errors='ignore')):
            if guid not in known and not guid.startswith('0000000000000000'):
                failures.append('Unresolved asset GUID in '+path.relative_to(root).as_posix()+': '+guid)
secret_re = re.compile(r'github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----')
for area in ['Assets','Packages','ProjectSettings','Docs','Tools']:
    for path in (root/area).rglob('*'):
        if not path.is_file():
            continue
        if path.stat().st_size > 45*1024*1024:
            failures.append('Unexpectedly large source asset: '+path.relative_to(root).as_posix())
        if path.suffix.lower() in {'.cs','.md','.json','.asset','.ps1','.yaml','.yml','.txt'}:
            if secret_re.search(path.read_text(encoding='utf-8',errors='ignore')):
                failures.append('Credential-like content in '+path.relative_to(root).as_posix())
for path in [root/'Packages/manifest.json',root/'Packages/packages-lock.json']:
    json.loads(path.read_text())
if failures:
    print('\n'.join(sorted(set(failures))))
    raise SystemExit(1)
print('PASS: required metadata, package JSON, source sizes and credential patterns checked.'+(' All serialized asset GUIDs resolve against project/package metadata.' if args.package_cache else ' Registry GUID resolution requires --package-cache after Unity import.'))
