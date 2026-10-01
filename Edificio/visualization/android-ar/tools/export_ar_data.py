"""Exporta una instantánea OpenSees y marcadores reproducibles; no recalcula la estructura."""
from pathlib import Path
import hashlib
import csv
import json
import math
import random
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[1]
BUILDING = PROJECT.parents[1]
ASSETS = PROJECT / 'app/src/main/assets'
MARKERS = PROJECT / 'markers'


def make_marker(tag, path):
    rng = random.Random(0xA6C0 + tag)
    image = Image.new('RGB', (512, 512), '#faf8f1')
    # These deterministic seeds need a richer low-frequency background to
    # pass Google's feature-quality threshold; keep the same image on paper/app.
    if tag in {26, 116, 228, 309, 577, 638, 699}:
        noise = Image.frombytes('L', (96, 96), bytes(rng.randrange(35, 235) for _ in range(96*96)))
        image = noise.resize((512, 512), Image.Resampling.BICUBIC).convert('RGB')
    draw = ImageDraw.Draw(image)
    # Asymmetric features at many spatial scales; each ID has a unique texture.
    for k in range(210):
        x, y = rng.randrange(14, 490), rng.randrange(14, 490)
        radius = rng.randrange(5, 38)
        shade = rng.randrange(15, 210)
        color = (shade, min(240, shade + rng.randrange(0, 30)), shade)
        if k % 3 == 0:
            draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=color)
        elif k % 3 == 1:
            draw.polygon([(x, y), (x+radius, y+radius*2), (x-radius*2, y+radius)], fill=color)
        else:
            draw.line((x, y, x+rng.randrange(-70, 70), y+rng.randrange(-70, 70)), fill=color, width=rng.randrange(2, 7))
    try:
        font = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 62)
        small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 23)
    except OSError:
        font = small = ImageFont.load_default()
    draw.rectangle((98, 206, 414, 302), fill='#101a29')
    draw.text((256, 251), f'ID {tag}', fill='white', anchor='mm', font=font)
    draw.rectangle((80, 14, 432, 52), fill='#faf8f1')
    draw.text((256, 33), 'EDIFICIO AR • ARRIBA', fill='#101a29', anchor='mm', font=small)
    image.save(path)


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    (ASSETS / 'markers').mkdir(exist_ok=True)
    MARKERS.mkdir(exist_ok=True)
    source = BUILDING / 'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json'
    model_path = BUILDING / 'results/modelo_3d_manual.json'
    model = json.loads(model_path.read_text(encoding='utf-8'))
    data = json.loads(source.read_text(encoding='utf-8'))
    nodes = {n['id']: n['xyz'] for n in data['nodes']}
    metadata = {e['id']: e for e in data['elementMetadata']}
    elements = {e['id']: e for e in model['elements']}
    cases = {c['name']: {b['id']: b for b in c['bars']} for c in data['cases'] if c['name'] in ('G', 'Q', 'EX', 'EY', 'R')}
    motions = {c['name']: {n['id']: n for n in c['nodes']} for c in data['cases'] if c['name'] in cases}
    loads_by_beam = {}
    for load in model.get('beam_load_cases', []):
        loads_by_beam.setdefault(int(load['beam_id']), []).append(load)
    parameters = json.loads((BUILDING/'data/parameters/parametros.json').read_text(encoding='utf-8'))
    combination = parameters['combinacion']
    pm_curve = []
    with (BUILDING/'results/PM_puntos.csv').open(encoding='utf-8-sig', newline='') as stream:
        for row in csv.DictReader(stream):
            pm_curve.append(dict(point=row['punto'], P_kN=float(row['P_kN']), M_kNm=float(row['M_kNm'])))
    members = []
    for bar in sorted(data['bars'], key=lambda b: b['id']):
        tag = bar['id']
        meta = metadata[tag]
        if not (meta['type'].startswith('BEAM') or 'COLUMN' in meta['type']):
            continue
        a, b = nodes[bar['i']], nodes[bar['j']]
        length = math.dist(a, b)
        assert length > 0 and all(tag in c for c in cases.values())
        e = elements[tag]
        section = meta['sectionData']
        dims = e.get('section_override', {})
        if 'COLUMN' in meta['type']:
            width = height = section.get('outer_width_m', 0.7)
        else:
            defaults = {'BEAM_SMALL': (0.3, 0.45), 'BEAM_VARIABLE': (0.3, 0.7), 'BEAM_40x60': (0.4, 0.6)}
            width, height = defaults.get(meta['type'], (0.6, 0.8))
        width, height = dims.get('b_m', width), dims.get('h_m', height)
        responses = {}
        for name, by_id in cases.items():
            response = by_id[tag]
            assert len(response['s']) >= 2
            fields = {key: response[key] for key in ('s', 'n', 'vy', 'vz', 't', 'my', 'mz')}
            assert all(len(v) == len(response['s']) and all(math.isfinite(x) for x in v) for v in fields.values())
            ni, nj = motions[name][bar['i']], motions[name][bar['j']]
            responses[name] = dict(**fields, ui=ni['u'], uj=nj['u'], ri=ni['r'], rj=nj['r'])
        tributary = loads_by_beam.get(tag, [])
        tributary_area = sum(float(row['tributary_area_m2']) for row in tributary)
        slab_dead = sum(float(row['dead_load_kN']) for row in tributary)
        slab_live = sum(float(row['live_load_kN']) for row in tributary)
        density_weight = (data.get('elementMetadata') and
                          (meta.get('materialData', {}).get('density_kg_m3', 0.0) * 9.80665 / 1000.0))
        unit_weight = density_weight if density_weight else float(parameters['peso_especifico_HA_kN_m3'])
        self_weight = float(section['A_m2']) * unit_weight * length
        applied = dict(G_kN=self_weight + slab_dead, Q_kN=slab_live, EX_kN=0.0, EY_kN=0.0,
                       R_kN=combination['G']*(self_weight + slab_dead) + combination['Q']*slab_live)
        load_info = dict(tributary_area_m2=tributary_area, associated_slab_ids=sorted({int(row['slab_id']) for row in tributary}),
                         slab_dead_load_kN=slab_dead, slab_live_load_kN=slab_live,
                         member_self_weight_kN=self_weight, applied_total_kN=applied)
        members.append(dict(id=tag, type=meta['type'], i=bar['i'], j=bar['j'], start=a, end=b,
                            length_m=length, width_m=width, height_m=height, local_x=bar['x'], local_y=bar['y'], local_z=bar['z'],
                            section=section, load_info=load_info, cases=responses,
                            pm_curve=pm_curve if meta['type'] == 'COLUMN' else [],
                            pm_note=('Curva nominal P-M de la columna HA de referencia 70x70 cm.' if meta['type'] == 'COLUMN'
                                     else 'No aplica: no existe una curva P-M verificada para este tipo de elemento.'),
                            marker=f'markers/element_{tag}.png', marker_width_m=0.20))
        make_marker(tag, ASSETS / f'markers/element_{tag}.png')
    # Results are kept at their source precision; signs/local basis are not altered.
    marker_hash = hashlib.sha256()
    for member in members:
        marker_hash.update(str(member['id']).encode('ascii'))
        marker_hash.update((ASSETS/member['marker']).read_bytes())
    snapshot = dict(schema=2, marker_set_sha256=marker_hash.hexdigest(), model_sha256=hashlib.sha256(model_path.read_bytes()).hexdigest(),
                    results_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), units='m, kN, kN*m',
                    origin='OpenSees, resultados precalculados', members=members)
    (ASSETS / 'structural_data.json').write_text(json.dumps(snapshot, separators=(',', ':'), ensure_ascii=False), encoding='utf-8')
    manifest = dict(width_m=0.20, model_sha256=snapshot['model_sha256'], elements=[dict(id=m['id'], type=m['type'], start=m['start'], end=m['end'], image='../app/src/main/assets/'+m['marker']) for m in members])
    (MARKERS / 'catalog.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    html = '''<!doctype html><html lang="es"><meta charset="utf-8"><title>Marcadores · Edificio AR</title>
<style>body{font:16px system-ui;background:#eef3f6;color:#101a29;margin:24px}button,input{padding:12px;font:inherit}article{background:white;padding:16px;margin:20px auto;max-width:760px}img{width:200mm;max-width:100%}h2{margin:0} .info{line-height:1.6}@media print{@page{size:A4;margin:5mm}body{background:white;margin:0}.controls{display:none}article{break-after:page;margin:0;padding:0;max-width:none}img{width:200mm;max-width:none}article:last-child{break-after:auto}}</style>
<div class="controls"><h1>Marcadores estructurales · Edificio AR</h1><p>Introduce IDs separados por coma. La imagen completa debe medir exactamente 20 × 20 cm. Imprime al 100%, sin «ajustar a página». No recortes ni tapes la textura. Coloca ARRIBA hacia arriba y el centro en el punto medio del elemento.</p><input id="ids" value="1,241,246"><button onclick="show()">Mostrar</button><button onclick="window.print()">Imprimir seleccionados</button><p id="msg"></p></div><main id="pages"></main><script>const members=CATALOG;function show(){const ids=document.getElementById('ids').value.split(',').map(x=>Number(x.trim()));const chosen=members.filter(x=>ids.includes(x.id));document.getElementById('msg').textContent=chosen.length+' marcadores encontrados de '+ids.length+' solicitados';document.getElementById('pages').innerHTML=chosen.map(m=>`<article><h2>EDIFICIO AR · ${m.type.includes('COLUMN')?'COLUMNA':'VIGA'} · ID ${m.id}</h2><p class="info">Nodo i → j: (${m.start.join(', ')}) → (${m.end.join(', ')}) m<br>Longitud: ${m.length_m.toFixed(3)} m · Sección: ${(m.width_m*100).toFixed(0)} × ${(m.height_m*100).toFixed(0)} cm<br>Imagen: 200 × 200 mm · ARRIBA indica orientación</p><img src="../app/src/main/assets/${m.marker}"><p>Resultados precalculados OpenSees · Identificación mediante imagen</p></article>`).join('')}show();</script></html>'''
    html = html.replace('CATALOG', json.dumps([{k: m[k] for k in ('id','type','start','end','length_m','width_m','height_m','marker')} for m in members]))
    (MARKERS / 'imprimir.html').write_text(html, encoding='utf-8')
    print(f'AR: {len(members)} miembros, {len(cases)} casos; marcadores 20 cm; hash {snapshot["model_sha256"][:12]}')


if __name__ == '__main__':
    main()
