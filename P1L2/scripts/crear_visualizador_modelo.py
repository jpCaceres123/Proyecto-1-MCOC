"""Genera un visualizador HTML autonomo del modelo estructural."""

from pathlib import Path
import json


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "outputs" / "modelo_3d_manual.json"
OUTPUT = ROOT / "visualizers" / "visualizador_modelo.html"


def main():
    data = json.loads(MODEL.read_text(encoding="utf-8"))
    payload = json.dumps(data, separators=(",", ":"))
    html = f"""<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Visualizador Modelo Edificio</title>
  <style>
    :root {{ color-scheme: light; font-family: Arial, sans-serif; }}
    body {{ margin: 0; background: #f4f6f8; color: #17202a; }}
    header {{ padding: 14px 18px; background: #17202a; color: white; }}
    h1 {{ margin: 0; font-size: 18px; }}
    main {{ display: grid; grid-template-columns: 320px 1fr; min-height: calc(100vh - 52px); }}
    aside {{ padding: 14px; background: white; border-right: 1px solid #d8dee4; overflow: auto; }}
    .viewer {{ position: relative; min-height: 620px; }}
    canvas {{ display: block; width: 100%; height: calc(100vh - 52px); background: #fbfcfe; }}
    label {{ display: block; margin: 8px 0; }}
    select, input[type="range"] {{ width: 100%; }}
    .checks label {{ display: flex; align-items: center; gap: 8px; }}
    .card {{ margin: 12px 0; padding: 10px; border: 1px solid #d8dee4; border-radius: 8px; background: #f8fafc; }}
    .legend span {{ display: inline-block; width: 11px; height: 11px; margin-right: 6px; border-radius: 2px; }}
    table {{ width: 100%; border-collapse: collapse; font-size: 12px; }}
    th, td {{ border-bottom: 1px solid #d8dee4; padding: 5px; text-align: left; }}
    button {{ width: 100%; margin-top: 8px; padding: 8px; border: 0; border-radius: 6px; background: #1f6feb; color: white; cursor: pointer; }}
     .hint {{ font-size: 12px; color: #57606a; line-height: 1.4; }}
     #info {{ overflow-x: auto; max-height: 52vh; }}
     #info table {{ min-width: 560px; }}
     .distribution {{ display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }}
     .distribution svg {{ width: 90px; height: 30px; border-bottom: 1px solid #57606a; overflow: visible; }}
     @media (max-width: 900px) {{ main {{ grid-template-columns: 1fr; }} canvas {{ height: 70vh; }} }}
  </style>
</head>
<body>
  <header><h1>Visualizador del Modelo Estructural</h1></header>
  <main>
    <aside>
      <div class="card">
        <label>Nivel Z [m]<select id="level"></select></label>
        <label>Opacidad losas<input id="slabOpacity" type="range" min="0" max="100" value="45"></label>
        <button id="fit">Centrar vista</button>
      </div>
      <div class="card checks">
        <label><input id="showSlabs" type="checkbox" checked> Losas</label>
        <label><input id="showBeams" type="checkbox" checked> Vigas</label>
        <label><input id="showColumns" type="checkbox" checked> Columnas</label>
        <label><input id="showWalls" type="checkbox" checked> Muros</label>
         <label><input id="showNodes" type="checkbox"> Nodos</label>
         <label><input id="showDisconnected" type="checkbox" checked> Desconectados</label>
         <label><input id="showLabels" type="checkbox" checked> IDs de losas</label>
      </div>
      <div class="card legend">
        <div><span style="background:#8ecae6"></span>Losa</div>
        <div><span style="background:#1f6feb"></span>Viga</div>
        <div><span style="background:#d73a49"></span>Columna</div>
         <div><span style="background:#6e7781"></span>Muro</div>
         <div><span style="background:#ffd33d"></span>Nodo</div>
         <div><span style="background:#d73a49"></span>Elemento desconectado</div>
         <div><span style="background:#2da44e"></span>Distribucion triangular</div>
         <div><span style="background:#8250df"></span>Distribucion trapezoidal</div>
      </div>
      <div class="card">
        <div class="hint">Vista en planta por nivel. Rueda del mouse: zoom. Arrastrar: mover. Click sobre una losa: ver datos.</div>
      </div>
       <div class="card" id="info">Selecciona una losa.</div>
       <div class="card" id="connectivity"></div>
    </aside>
    <section class="viewer"><canvas id="canvas"></canvas></section>
  </main>
  <script>
    const model = {payload};
     const nodes = new Map(model.nodes.map(n => [n.id, n]));
     const endpointIds = new Set(model.elements.flatMap(e => [e.i, e.j]));
     const adjacency = new Map([...endpointIds].map(id => [id, new Set()]));
     for (const e of model.elements) {{ adjacency.get(e.i).add(e.j); adjacency.get(e.j).add(e.i); }}
     const supported = new Set(model.nodes.filter(n => n.restraint).map(n => n.id));
     const seen = new Set(), disconnectedNodes = new Set();
     for (const start of endpointIds) {{
       if (seen.has(start)) continue;
       const stack = [start], component = new Set(); seen.add(start);
       while (stack.length) {{ const id = stack.pop(); component.add(id); for (const next of adjacency.get(id)) if (!seen.has(next)) {{ seen.add(next); stack.push(next); }} }}
       if (![...component].some(id => supported.has(id))) for (const id of component) disconnectedNodes.add(id);
     }}
     const disconnectedElements = new Set(model.elements.filter(e => disconnectedNodes.has(e.i) && disconnectedNodes.has(e.j)).map(e => e.id));
     const levels = [...new Set(model.nodes.map(n => Number(n.z_m.toFixed(3))))].sort((a,b) => a-b);
    const levelSelect = document.getElementById('level');
    for (const z of levels) {{
       const opt = document.createElement('option'); opt.value = z; opt.textContent = z === 0 ? 'Base (z=0.00 m)' : z.toFixed(2); levelSelect.appendChild(opt);
    }}
    const canvas = document.getElementById('canvas');
    const ctx = canvas.getContext('2d');
    const state = {{ scale: 12, ox: 60, oy: 60, drag: false, last: null, selectedSlab: null }};

    function resize() {{ canvas.width = canvas.clientWidth * devicePixelRatio; canvas.height = canvas.clientHeight * devicePixelRatio; draw(); }}
    function sx(x) {{ return state.ox + x * state.scale; }}
    function sy(y) {{ return canvas.height / devicePixelRatio - (state.oy + y * state.scale); }}
    function wx(x) {{ return (x - state.ox) / state.scale; }}
    function wy(y) {{ return ((canvas.height / devicePixelRatio - y) - state.oy) / state.scale; }}
    function sameLevel(z, value) {{ return Math.abs(Number(z) - Number(value)) < 1e-6; }}
    function currentLevel() {{ return Number(levelSelect.value); }}

    function fit() {{
      const z = currentLevel();
      const pts = model.nodes.filter(n => sameLevel(n.z_m, z));
      if (!pts.length) return;
      const minX = Math.min(...pts.map(p => p.x_m)), maxX = Math.max(...pts.map(p => p.x_m));
      const minY = Math.min(...pts.map(p => p.y_m)), maxY = Math.max(...pts.map(p => p.y_m));
      const w = canvas.clientWidth, h = canvas.clientHeight;
      state.scale = Math.min((w - 80) / Math.max(maxX - minX, 1), (h - 80) / Math.max(maxY - minY, 1));
      state.ox = 40 - minX * state.scale;
      state.oy = 40 - minY * state.scale;
      draw();
    }}

     function polygonPath(points) {{
       ctx.beginPath();
       ctx.moveTo(sx(points[0].x_m), sy(points[0].y_m));
       for (const p of points.slice(1)) ctx.lineTo(sx(p.x_m), sy(p.y_m));
       ctx.closePath();
     }}

     function drawSelectedDistribution(slab) {{
       const points = slab.coordinates;
       const x0 = Math.min(...points.map(p => p.x_m)), x1 = Math.max(...points.map(p => p.x_m));
       const y0 = Math.min(...points.map(p => p.y_m)), y1 = Math.max(...points.map(p => p.y_m));
       const xm = (x0 + x1) / 2, ym = (y0 + y1) / 2;
       const depthX = (y1 - y0) / 2, depthY = (x1 - x0) / 2;
        for (const load of slab.tributary_loads) {{
          const triangular = load.distribution === 'triangular';
          const color = triangular ? '#2da44e' : '#8250df';
          const partitionRegion = (slab.global_partition && slab.global_partition.regions || []).find(region => region.edge === load.edge);
          let shape;
          if (partitionRegion) {{
            shape = partitionRegion.polygon;
          }} else if (load.edge === 'bottom' || load.edge === 'top') {{
           const y = load.edge === 'bottom' ? y0 : y1;
           const inward = load.edge === 'bottom' ? ym : ym;
           shape = triangular
             ? [{{x_m:x0,y_m:y}}, {{x_m:x1,y_m:y}}, {{x_m:load.edge === 'bottom' ? x0 + depthX : x1 - depthX,y_m:inward}}]
             : [{{x_m:x0,y_m:y}}, {{x_m:x1,y_m:y}}, {{x_m:x1 - depthX,y_m:inward}}, {{x_m:x0 + depthX,y_m:inward}}];
         }} else {{
           const x = load.edge === 'left' ? x0 : x1;
           shape = triangular
             ? [{{x_m:x,y_m:y0}}, {{x_m:x,y_m:y1}}, {{x_m:x_m,y_m:ym}}]
             : [{{x_m:x,y_m:y0}}, {{x_m:x,y_m:y1}}, {{x_m:x_m,y_m:y1 - depthY}}, {{x_m:x_m,y_m:y0 + depthY}}];
         }}
         polygonPath(shape);
         ctx.fillStyle = triangular ? 'rgba(45,164,78,0.55)' : 'rgba(130,80,223,0.55)';
         ctx.strokeStyle = color;
         ctx.lineWidth = 1.5;
         ctx.fill(); ctx.stroke();
         const label = triangular ? 'T' : 'Tr';
         let labelX = xm, labelY = ym;
         if (load.edge === 'bottom') labelY = (y0 + ym) / 2;
         if (load.edge === 'top') labelY = (y1 + ym) / 2;
         if (load.edge === 'left') labelX = (x0 + xm) / 2;
         if (load.edge === 'right') labelX = (x1 + xm) / 2;
         ctx.fillStyle = color;
         ctx.font = 'bold 11px Arial';
         ctx.fillText(label, sx(labelX) - 7, sy(labelY) + 4);
       }}
     }}

    function drawSlabs(z) {{
      if (!document.getElementById('showSlabs').checked) return;
      const alpha = Number(document.getElementById('slabOpacity').value) / 100;
      for (const slab of model.slabs.filter(s => sameLevel(s.z_m, z))) {{
        polygonPath(slab.coordinates);
         ctx.fillStyle = `rgba(142,202,230,${{alpha}})`;
         ctx.strokeStyle = '#4696b4';
         ctx.lineWidth = 1;
         if (slab.id !== state.selectedSlab) ctx.fill();
         ctx.stroke();
        if (document.getElementById('showLabels').checked) {{
          const cx = slab.coordinates.reduce((s,p)=>s+p.x_m,0)/4;
          const cy = slab.coordinates.reduce((s,p)=>s+p.y_m,0)/4;
          ctx.fillStyle = '#17202a'; ctx.font = '11px Arial'; ctx.fillText(String(slab.id), sx(cx)-10, sy(cy)+4);
        }}
      }}
    }}

     function drawLine(a, b, color, width) {{
       ctx.beginPath(); ctx.moveTo(sx(a.x_m), sy(a.y_m)); ctx.lineTo(sx(b.x_m), sy(b.y_m));
       ctx.strokeStyle = color; ctx.lineWidth = width; ctx.stroke();
     }}

     function voidPath(voidArea) {{
       polygonPath([
         {{x_m:voidArea.x_min_m,y_m:voidArea.y_min_m}},
         {{x_m:voidArea.x_max_m,y_m:voidArea.y_min_m}},
         {{x_m:voidArea.x_max_m,y_m:voidArea.y_max_m}},
         {{x_m:voidArea.x_min_m,y_m:voidArea.y_max_m}}
       ]);
     }}

     function drawVoids(z) {{
       for (const slab of model.slabs.filter(s => sameLevel(s.z_m, z))) {{
         for (const voidArea of slab.voids || []) {{
           voidPath(voidArea);
           ctx.fillStyle = '#fbfcfe';
           ctx.fill();
           ctx.strokeStyle = '#8c959f';
           ctx.setLineDash([5, 4]);
           ctx.lineWidth = 1;
           ctx.stroke();
           ctx.setLineDash([]);
         }}
       }}
     }}

     function drawSelected45Lines(slab) {{
       for (const line of (slab.global_partition && slab.global_partition.construction_lines_45) || []) {{
         ctx.beginPath();
         ctx.moveTo(sx(line.start.x_m), sy(line.start.y_m));
         ctx.lineTo(sx(line.end.x_m), sy(line.end.y_m));
         ctx.strokeStyle = line.kind === 'void_partition' ? '#57606a' : '#2da44e';
         ctx.lineWidth = line.kind === 'void_partition' ? 1 : 1.5;
         ctx.setLineDash(line.kind === 'void_partition' ? [3, 3] : []);
         ctx.stroke();
         ctx.setLineDash([]);
       }}
     }}

     function drawElements(z) {{
       if (document.getElementById('showBeams').checked) {{
        for (const e of model.elements.filter(e => e.type.startsWith('BEAM'))) {{
           const a = nodes.get(e.i), b = nodes.get(e.j); if (a && b && sameLevel(a.z_m,z) && sameLevel(b.z_m,z)) drawLine(a,b,document.getElementById('showDisconnected').checked && disconnectedElements.has(e.id) ? '#d73a49' : '#1f6feb',document.getElementById('showDisconnected').checked && disconnectedElements.has(e.id) ? 4 : 2);
       }}
     }}

       if (document.getElementById('showColumns').checked) {{
        for (const e of model.elements.filter(e => e.type === 'COLUMN')) {{
          const a = nodes.get(e.i), b = nodes.get(e.j); if (a && b && a.z_m <= z && b.z_m >= z) {{
             ctx.beginPath(); ctx.arc(sx(a.x_m), sy(a.y_m), 4, 0, Math.PI*2); ctx.fillStyle = document.getElementById('showDisconnected').checked && disconnectedElements.has(e.id) ? '#d73a49' : '#2da44e'; ctx.fill();
          }}
        }}
      }}
      if (document.getElementById('showWalls').checked) {{
        for (const w of model.walls || []) {{ if (w.z_i_m <= z && w.z_j_m >= z) drawLine({{x_m:w.x_i_m,y_m:w.y_i_m}}, {{x_m:w.x_j_m,y_m:w.y_j_m}}, '#6e7781', Math.max(2, w.thickness_m*state.scale)); }}
      }}
      if (document.getElementById('showNodes').checked) {{
        for (const n of model.nodes.filter(n => sameLevel(n.z_m,z))) {{ ctx.beginPath(); ctx.arc(sx(n.x_m), sy(n.y_m), 2.5, 0, Math.PI*2); ctx.fillStyle = '#ffd33d'; ctx.fill(); }}
       }}
     }}

     function drawSelectedSlab(z) {{
       const slab = model.slabs.find(s => s.id === state.selectedSlab && sameLevel(s.z_m, z));
       if (!slab) return;
       polygonPath(slab.coordinates);
       ctx.strokeStyle = '#bf8700';
       ctx.lineWidth = 2;
       ctx.stroke();
       if (slab.tributary_loads && slab.tributary_loads.length) {{
         drawSelectedDistribution(slab);
       }} else {{
         ctx.fillStyle = '#57606a';
         ctx.font = 'bold 12px Arial';
         ctx.fillText('Sin distribucion asignada', sx((slab.coordinates[0].x_m + slab.coordinates[2].x_m) / 2) - 65, sy((slab.coordinates[0].y_m + slab.coordinates[2].y_m) / 2));
       }}
       drawSelected45Lines(slab);
       for (const voidArea of slab.voids || []) {{
         voidPath(voidArea);
         ctx.fillStyle = '#fbfcfe';
         ctx.fill();
         ctx.strokeStyle = '#8c959f';
         ctx.setLineDash([5, 4]);
         ctx.stroke();
         ctx.setLineDash([]);
       }}
     }}

     function draw() {{
      ctx.setTransform(devicePixelRatio,0,0,devicePixelRatio,0,0);
      ctx.clearRect(0,0,canvas.clientWidth,canvas.clientHeight);
       drawSlabs(currentLevel());
       drawVoids(currentLevel());
       drawElements(currentLevel());
       drawSelectedSlab(currentLevel());
       document.getElementById('connectivity').innerHTML = `<b>Conectividad</b><br>Nodos desconectados: ${{disconnectedNodes.size}}<br>Elementos desconectados: ${{disconnectedElements.size}}<br><span class="hint">Rojo = componente sin camino hacia un apoyo.</span>`;
    }}

    function pointInPolygon(x, y, poly) {{
      let inside = false;
      for (let i=0,j=poly.length-1; i<poly.length; j=i++) {{
        const xi=poly[i].x_m, yi=poly[i].y_m, xj=poly[j].x_m, yj=poly[j].y_m;
        const intersect = ((yi>y)!==(yj>y)) && (x < (xj-xi)*(y-yi)/(yj-yi)+xi);
        if (intersect) inside = !inside;
      }}
      return inside;
    }}

     function showInfo(slab) {{
       const info = document.getElementById('info');
       if (!slab) {{ info.textContent = 'Selecciona una losa.'; return; }}
       const distributionGraphic = type => type === 'triangular'
         ? `<svg viewBox="0 0 100 35" role="img" aria-label="triangular"><polygon points="0,34 50,3 100,34" fill="#2da44e" fill-opacity="0.55" stroke="#2da44e" stroke-width="2" /></svg>`
         : `<svg viewBox="0 0 100 35" role="img" aria-label="trapezoidal"><polygon points="0,34 22,7 78,7 100,34" fill="#8250df" fill-opacity="0.35" stroke="#8250df" stroke-width="2" /></svg>`;
       const loads = slab.tributary_loads.map(l => `<tr><td>${{l.edge}}</td><td>${{l.beam_ids.join(',')}}</td><td>${{l.tributary_area_m2.toFixed(2)}} m²</td><td><span class="distribution">${{distributionGraphic(l.distribution)}} ${{l.distribution}}</span></td><td>${{l.w_start_kN_m.toFixed(2)}} / ${{l.w_max_kN_m.toFixed(2)}} / ${{l.w_end_kN_m.toFixed(2)}} kN/m</td></tr>`).join('');
       const unassigned = (model.unassigned_load_slabs || []).some(item => item.slab_id === slab.id);
       const status = unassigned ? '<br><b style="color:#b35900">Zona de carga: no asignada; revisar si corresponde a un vacio.</b>' : '<br><b style="color:#2da44e">Zona de carga: asignada.</b>';
       const partition = slab.global_partition || {{}};
       info.innerHTML = `<b>Paño de losa ${{slab.panel_id || slab.id}}</b>${{status}}<br>Metodo: particion global a 45°<br>Control particion: ${{partition.area_check_m2 ?? 'n/d'}} m²; solape: ${{partition.overlap_m2 ?? 'n/d'}} m²<br>Z=${{slab.z_m.toFixed(2)}} m<br>Area cargada=${{slab.area_m2.toFixed(2)}} m²<br>Fronteras=${{(slab.boundary_elements || []).join(', ')}}<br>Dims=${{slab.dimensions_m.lx.toFixed(2)}} x ${{slab.dimensions_m.ly.toFixed(2)}} m<br>Nodos=${{slab.node_ids.join(', ')}}<table><thead><tr><th>Borde</th><th>Viga/muro</th><th>Area tributaria</th><th>Distribucion</th><th>w inicio / max / final</th></tr></thead><tbody>${{loads}}</tbody></table>`;
     }}

    canvas.addEventListener('mousedown', e => {{ state.drag = true; state.last = [e.clientX,e.clientY]; }});
    addEventListener('mouseup', () => state.drag = false);
    canvas.addEventListener('mousemove', e => {{ if (!state.drag) return; state.ox += e.clientX-state.last[0]; state.oy -= e.clientY-state.last[1]; state.last = [e.clientX,e.clientY]; draw(); }});
    canvas.addEventListener('wheel', e => {{ e.preventDefault(); const k = e.deltaY < 0 ? 1.12 : 0.89; state.scale *= k; draw(); }}, {{passive:false}});
    canvas.addEventListener('click', e => {{
      if (state.drag) return;
      const rect = canvas.getBoundingClientRect(); const x = wx(e.clientX-rect.left), y = wy(e.clientY-rect.top);
      const slabs = model.slabs.filter(s => sameLevel(s.z_m,currentLevel())).reverse();
      const slab = slabs.find(s => pointInPolygon(x,y,s.coordinates));
      state.selectedSlab = slab ? slab.id : null; showInfo(slab); draw();
    }});
     for (const id of ['level','slabOpacity','showSlabs','showBeams','showColumns','showWalls','showNodes','showDisconnected','showLabels']) document.getElementById(id).addEventListener('input', draw);
    document.getElementById('level').addEventListener('change', () => {{ state.selectedSlab = null; showInfo(null); fit(); }});
    document.getElementById('fit').addEventListener('click', fit);
    addEventListener('resize', resize);
    resize(); fit();
  </script>
</body>
</html>
"""
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(html, encoding="utf-8")
    print(f"Visualizador creado: {OUTPUT.name}")


if __name__ == "__main__":
    main()
