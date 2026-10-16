#!/usr/bin/env python3
"""Package / historic fixture sanity only; does NOT execute or validate C# code."""
import hashlib
import json
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[1]
catalog_bytes = (root / 'IndependentSolver/IO/HoleShapes.json').read_bytes()
hash_ = hashlib.sha256(catalog_bytes).hexdigest()
shapes = {x['holeType']: x['footprint'] for x in json.loads(catalog_bytes)['shapes']}
for kind in ('RegionRelease', 'QueueRevisitParking'):
    req = json.loads((root / 'LevelGenerator.Examples' / f'{kind}.request.json').read_text())
    assert req['catalogHash'] == hash_
    assert req['targetPolicy']['minHoleFootprint'] == 25
    assert req['targetPolicy']['maxHoleFootprint'] == 32
    assert req['targetPolicy']['targetHoleFootprint'] is None
    assert req['intent']['pattern'] == kind
    assert req['presetRevision'] == '2026-10-15-footprint-25-32-v1'
    assert all(req['budgets'][x] > 0 for x in ('maxAttempts', 'perSolveMs','totalRunMs'))
    print(f'PASS request {kind}: footprint range 25..32; hash {hash_[:12]}...')
for level_id in ('00115', '00098', '00129'):
    doc = json.loads((root / 'Levels' / f'Level{level_id}.json').read_text())
    area = sum(len(shapes[h['holeType']]) for h in doc['holeInfos'])
    assert 25 <= area <= 32, (level_id, area)
    demand, supply = Counter(), Counter()
    for h in doc['holeInfos']:
        demand.update(dict(zip(h['colorIds'], h['numOfCats'])))
    supply.update(c['colorId'] for c in doc['catInfos'])
    for box in doc['catBoxInfos']:
        supply.update(c['colorId'] for c in box['colorIds'])
    assert demand == supply, (level_id, demand, supply)
    print(f'PASS reference Level{level_id}: footprint={area}, supply/demand balanced')
for folder in ('LevelGenerator.Core', 'LevelGenerator.Cli', 'LevelGenerator.Tests'):
    assert (root/folder/f'{folder}.csproj').exists()
print('Package/config/reference static checks passed (C# remains uncompiled here).')
