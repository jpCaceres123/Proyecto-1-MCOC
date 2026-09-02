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
        <label><input id="showLabels" type="checkbox" checked> IDs de losas</label>
      </div>
      <div class="card legend">
        <div><span style="background:#8ecae6"></span>Losa</div>
        <div><span style="background:#1f6feb"></span>Viga</div>
        <div><span style="background:#d73a49"></span>Columna</div>
        <div><span style="background:#6e7781"></span>Muro</div>
        <div><span style="background:#ffd33d"></span>Nodo</div>
      </div>
      <div class="card">
        <div class="hint">Vista en planta por nivel. Rueda del mouse: zoom. Arrastrar: mover. Click sobre una losa: ver datos.</div>
      </div>
      <div class="card" id="info">Selecciona una losa.</div>
    </aside>
    <section class="viewer"><canvas id="canvas"></canvas></section>
  </main>
  <script>
    const model = {payload};
    const nodes = new Map(model.nodes.map(n => [n.id, n]));
    const levels = [...new Set(model.nodes.map(n => Number(n.z_m.toFixed(3))))].sort((a,b) => a-b).filter(z => z > 0);
    const levelSelect = document.getElementById('level');
    for (const z of levels) {{
      const opt = document.createElement('option'); opt.value = z; opt.textContent = z.toFixed(2); levelSelect.appendChild(opt);
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

    function drawSlabs(z) {{
      if (!document.getElementById('showSlabs').checked) return;
      const alpha = Number(document.getElementById('slabOpacity').value) / 100;
      for (const slab of model.slabs.filter(s => sameLevel(s.z_m, z))) {{
        polygonPath(slab.coordinates);
        ctx.fillStyle = slab.id === state.selectedSlab ? `rgba(255,179,0,${{Math.max(alpha,0.7)}})` : `rgba(142,202,230,${{alpha}})`;
        ctx.strokeStyle = slab.id === state.selectedSlab ? '#bf8700' : '#4696b4';
        ctx.lineWidth = slab.id === state.selectedSlab ? 2 : 1;
        ctx.fill(); ctx.stroke();
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

    function drawElements(z) {{
      if (document.getElementById('showBeams').checked) {{
        for (const e of model.elements.filter(e => e.type.startsWith('BEAM'))) {{
          const a = nodes.get(e.i), b = nodes.get(e.j); if (a && b && sameLevel(a.z_m,z) && sameLevel(b.z_m,z)) drawLine(a,b,'#1f6feb',2);
        }}
      }}
      if (document.getElementById('showColumns').checked) {{
        for (const e of model.elements.filter(e => e.type === 'COLUMN')) {{
          const a = nodes.get(e.i), b = nodes.get(e.j); if (a && b && a.z_m <= z && b.z_m >= z) {{
            ctx.beginPath(); ctx.arc(sx(a.x_m), sy(a.y_m), 4, 0, Math.PI*2); ctx.fillStyle = '#d73a49'; ctx.fill();
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

    function draw() {{
      ctx.setTransform(devicePixelRatio,0,0,devicePixelRatio,0,0);
      ctx.clearRect(0,0,canvas.clientWidth,canvas.clientHeight);
      drawSlabs(currentLevel());
      drawElements(currentLevel());
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
      const loads = slab.tributary_loads.map(l => `<tr><td>${{l.edge}}</td><td>${{l.beam_ids.join(',')}}</td><td>${{l.tributary_area_m2.toFixed(2)}}</td><td>${{l.distribution}}</td><td>${{l.w_start_kN_m.toFixed(2)}}/${{l.w_max_kN_m.toFixed(2)}}/${{l.w_end_kN_m.toFixed(2)}}</td></tr>`).join('');
      info.innerHTML = `<b>Losa ${{slab.id}}</b><br>Z=${{slab.z_m.toFixed(2)}} m<br>Area=${{slab.area_m2.toFixed(2)}} m²<br>Dims=${{slab.dimensions_m.lx.toFixed(2)}} x ${{slab.dimensions_m.ly.toFixed(2)}} m<br>Nodos=${{slab.node_ids.join(', ')}}<table><thead><tr><th>Borde</th><th>Viga</th><th>Atrib</th><th>Tipo</th><th>w i/max/f</th></tr></thead><tbody>${{loads}}</tbody></table>`;
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
    for (const id of ['level','slabOpacity','showSlabs','showBeams','showColumns','showWalls','showNodes','showLabels']) document.getElementById(id).addEventListener('input', draw);
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
