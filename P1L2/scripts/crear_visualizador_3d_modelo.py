"""Genera un visualizador 3D HTML autonomo del modelo estructural."""

from pathlib import Path
import json


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "outputs" / "modelo_3d_manual.json"
OUTPUT = ROOT / "visualizers" / "visualizador_modelo_3d.html"


HTML_TEMPLATE = r'''<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Visualizador 3D Modelo Edificio</title>
  <style>
    body { margin: 0; font-family: Arial, sans-serif; background: #0f172a; color: #e5e7eb; overflow: hidden; }
    header { height: 44px; display: flex; align-items: center; justify-content: space-between; padding: 0 14px; background: #111827; border-bottom: 1px solid #334155; }
    h1 { margin: 0; font-size: 16px; }
    main { display: grid; grid-template-columns: 320px 1fr; height: calc(100vh - 45px); }
    aside { overflow: auto; padding: 12px; background: #111827; border-right: 1px solid #334155; }
    canvas { display: block; width: 100%; height: calc(100vh - 45px); background: linear-gradient(#e8eef7, #f8fafc); }
    .card { margin: 0 0 12px; padding: 10px; border: 1px solid #334155; border-radius: 8px; background: #1f2937; }
    label { display: block; margin: 8px 0; font-size: 13px; }
    select, input[type="range"] { width: 100%; }
    input[type="checkbox"] { margin-right: 8px; }
    button { width: 100%; padding: 8px; margin-top: 8px; border: 0; border-radius: 6px; background: #2563eb; color: white; cursor: pointer; }
    .legend div { margin: 6px 0; font-size: 13px; }
    .legend span { display: inline-block; width: 12px; height: 12px; margin-right: 8px; border-radius: 2px; }
    .hint { font-size: 12px; color: #cbd5e1; line-height: 1.45; }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    th, td { border-bottom: 1px solid #334155; padding: 4px; text-align: left; }
    @media (max-width: 900px) { main { grid-template-columns: 1fr; } aside { max-height: 42vh; } canvas { height: 58vh; } body { overflow: auto; } }
  </style>
</head>
<body>
  <header>
    <h1>Visualizador 3D del Modelo Estructural</h1>
    <span id="stats"></span>
  </header>
  <main>
    <aside>
      <div class="card">
        <label>Nivel visible
          <select id="level"><option value="all">Todos</option></select>
        </label>
        <label>Zoom<input id="zoom" type="range" min="4" max="45" value="16"></label>
        <label>Opacidad losas<input id="slabOpacity" type="range" min="5" max="100" value="45"></label>
        <button id="reset">Reiniciar vista</button>
      </div>
      <div class="card">
        <label><input id="showSlabs" type="checkbox" checked>Losas</label>
        <label><input id="showBeams" type="checkbox" checked>Vigas</label>
        <label><input id="showColumns" type="checkbox" checked>Columnas</label>
        <label><input id="showWalls" type="checkbox" checked>Muros</label>
        <label><input id="showNodes" type="checkbox">Nodos</label>
        <label><input id="showLabels" type="checkbox">IDs de losas</label>
        <label><input id="showTributary" type="checkbox" checked>Area tributaria losa seleccionada</label>
        <label><input id="showAxes" type="checkbox" checked>Ejes XYZ</label>
      </div>
      <div class="card legend">
        <div><span style="background:#38bdf8"></span>Losas</div>
        <div><span style="background:#2563eb"></span>Vigas</div>
        <div><span style="background:#dc2626"></span>Columnas</div>
        <div><span style="background:#64748b"></span>Muros</div>
        <div><span style="background:#facc15"></span>Nodos</div>
      </div>
      <div class="card hint">
        Arrastrar: rotar vista.<br>
        Shift + arrastrar: mover.<br>
        Rueda: zoom.<br>
        Click sobre una losa: ver datos.
      </div>
      <div class="card" id="info">Selecciona una losa.</div>
    </aside>
    <canvas id="canvas"></canvas>
  </main>
  <script>
    const model = __MODEL_JSON__;
    const nodes = new Map(model.nodes.map(n => [n.id, n]));
    const levels = [...new Set(model.nodes.map(n => Number(n.z_m.toFixed(3))))].sort((a,b) => a-b);
    const levelSelect = document.getElementById('level');
    for (const z of levels) {
      const option = document.createElement('option');
      option.value = z;
      option.textContent = z.toFixed(2) + ' m';
      levelSelect.appendChild(option);
    }
    document.getElementById('stats').textContent = `${model.nodes.length} nodos | ${model.elements.length} elementos | ${model.slabs.length} losas`;

    const canvas = document.getElementById('canvas');
    const ctx = canvas.getContext('2d');
    const state = { ax: -0.72, az: -0.58, scale: 16, panX: 0, panY: 20, dragging: false, panning: false, last: null, selectedSlab: null };

    function resize() {
      canvas.width = Math.max(1, canvas.clientWidth) * devicePixelRatio;
      canvas.height = Math.max(1, canvas.clientHeight) * devicePixelRatio;
      draw();
    }

    function centerPoint() {
      const xs = model.nodes.map(n => n.x_m);
      const ys = model.nodes.map(n => n.y_m);
      const zs = model.nodes.map(n => n.z_m);
      return { x: (Math.min(...xs) + Math.max(...xs)) / 2, y: (Math.min(...ys) + Math.max(...ys)) / 2, z: (Math.min(...zs) + Math.max(...zs)) / 2 };
    }
    const center = centerPoint();

    function project(p) {
      let x = p.x_m - center.x;
      let y = p.y_m - center.y;
      let z = p.z_m - center.z;
      const caz = Math.cos(state.az), saz = Math.sin(state.az);
      const cax = Math.cos(state.ax), sax = Math.sin(state.ax);
      const x1 = x * caz - y * saz;
      const y1 = x * saz + y * caz;
      const z1 = z;
      const y2 = y1 * cax - z1 * sax;
      const z2 = y1 * sax + z1 * cax;
      return {
        x: canvas.clientWidth / 2 + state.panX + x1 * state.scale,
        y: canvas.clientHeight / 2 + state.panY - z2 * state.scale,
        depth: y2
      };
    }

    function visibleAtLevel(z) {
      const selected = levelSelect.value;
      return selected === 'all' || Math.abs(Number(selected) - Number(z)) < 1e-6;
    }

    function line(a, b, color, width) {
      const pa = project(a), pb = project(b);
      ctx.beginPath();
      ctx.moveTo(pa.x, pa.y);
      ctx.lineTo(pb.x, pb.y);
      ctx.strokeStyle = color;
      ctx.lineWidth = width;
      ctx.stroke();
    }

    function slabDepth(slab) {
      return slab.coordinates.reduce((sum, p) => sum + project(p).depth, 0) / slab.coordinates.length;
    }

    function drawSlab(slab) {
      const pts = slab.coordinates.map(project);
      ctx.beginPath();
      ctx.moveTo(pts[0].x, pts[0].y);
      for (const p of pts.slice(1)) ctx.lineTo(p.x, p.y);
      ctx.closePath();
      const alpha = Number(document.getElementById('slabOpacity').value) / 100;
      ctx.fillStyle = slab.id === state.selectedSlab ? `rgba(245,158,11,${Math.max(alpha, 0.75)})` : `rgba(56,189,248,${alpha})`;
      ctx.strokeStyle = slab.id === state.selectedSlab ? '#92400e' : '#0284c7';
      ctx.lineWidth = slab.id === state.selectedSlab ? 2.4 : 1;
      ctx.fill();
      ctx.stroke();
      if (document.getElementById('showLabels').checked) {
        const c = slab.coordinates.reduce((acc, p) => ({ x_m: acc.x_m + p.x_m / 4, y_m: acc.y_m + p.y_m / 4, z_m: acc.z_m + p.z_m / 4 }), { x_m: 0, y_m: 0, z_m: 0 });
        const pc = project(c);
        ctx.fillStyle = '#111827';
        ctx.font = '11px Arial';
        ctx.fillText(String(slab.id), pc.x - 10, pc.y + 4);
      }
    }

    function tributaryRegions(slab) {
      const [p0, p1, p2, p3] = slab.coordinates;
      const x0 = p0.x_m, x1 = p1.x_m, y0 = p0.y_m, y1 = p2.y_m, z = p0.z_m;
      const lx = Math.abs(x1 - x0), ly = Math.abs(y1 - y0);
      const xm = (x0 + x1) / 2, ym = (y0 + y1) / 2;
      const ratio = Math.max(lx, ly) / Math.min(lx, ly);
      const regions = [];
      const poly = (edge, points) => ({ edge, points: points.map(([x,y]) => ({ x_m: x, y_m: y, z_m: z + 0.035 })) });

      if (ratio >= 2.0) {
        if (lx >= ly) {
          regions.push(poly('bottom', [[x0,y0], [x1,y0], [x1,ym], [x0,ym]]));
          regions.push(poly('top', [[x0,ym], [x1,ym], [x1,y1], [x0,y1]]));
        } else {
          regions.push(poly('left', [[x0,y0], [xm,y0], [xm,y1], [x0,y1]]));
          regions.push(poly('right', [[xm,y0], [x1,y0], [x1,y1], [xm,y1]]));
        }
        return regions;
      }

      if (lx >= ly) {
        const d = ly / 2;
        regions.push(poly('left', [[x0,y0], [x0,y1], [x0 + d,ym]]));
        regions.push(poly('right', [[x1,y0], [x1 - d,ym], [x1,y1]]));
        regions.push(poly('bottom', [[x0,y0], [x1,y0], [x1 - d,ym], [x0 + d,ym]]));
        regions.push(poly('top', [[x0,y1], [x0 + d,ym], [x1 - d,ym], [x1,y1]]));
      } else {
        const d = lx / 2;
        regions.push(poly('bottom', [[x0,y0], [x1,y0], [xm,y0 + d]]));
        regions.push(poly('top', [[x0,y1], [xm,y1 - d], [x1,y1]]));
        regions.push(poly('left', [[x0,y0], [xm,y0 + d], [xm,y1 - d], [x0,y1]]));
        regions.push(poly('right', [[x1,y0], [x1,y1], [xm,y1 - d], [xm,y0 + d]]));
      }
      return regions;
    }

    function drawTributaryRegions(slab) {
      if (!document.getElementById('showTributary').checked || !slab) return;
      const colors = {
        bottom: 'rgba(34,197,94,0.55)',
        top: 'rgba(168,85,247,0.55)',
        left: 'rgba(249,115,22,0.55)',
        right: 'rgba(236,72,153,0.55)'
      };
      for (const region of tributaryRegions(slab)) {
        const pts = region.points.map(project);
        ctx.beginPath();
        ctx.moveTo(pts[0].x, pts[0].y);
        for (const p of pts.slice(1)) ctx.lineTo(p.x, p.y);
        ctx.closePath();
        ctx.fillStyle = colors[region.edge] || 'rgba(250,204,21,0.5)';
        ctx.strokeStyle = '#111827';
        ctx.lineWidth = 1.4;
        ctx.fill();
        ctx.stroke();

        const centroid = region.points.reduce((acc, p) => ({ x_m: acc.x_m + p.x_m / region.points.length, y_m: acc.y_m + p.y_m / region.points.length, z_m: acc.z_m + p.z_m / region.points.length }), { x_m: 0, y_m: 0, z_m: 0 });
        const pc = project(centroid);
        const load = slab.tributary_loads.find(l => l.edge === region.edge);
        ctx.fillStyle = '#0f172a';
        ctx.font = '11px Arial';
        ctx.fillText(`${region.edge}${load ? ' V:' + load.beam_ids.join(',') : ''}`, pc.x - 28, pc.y);
      }
    }

    function drawWall(wall) {
      const a = { x_m: wall.x_i_m, y_m: wall.y_i_m, z_m: wall.z_i_m };
      const b = { x_m: wall.x_j_m, y_m: wall.y_j_m, z_m: wall.z_i_m };
      const c = { x_m: wall.x_j_m, y_m: wall.y_j_m, z_m: wall.z_j_m };
      const d = { x_m: wall.x_i_m, y_m: wall.y_i_m, z_m: wall.z_j_m };
      const pts = [a, b, c, d].map(project);
      ctx.beginPath();
      ctx.moveTo(pts[0].x, pts[0].y);
      for (const p of pts.slice(1)) ctx.lineTo(p.x, p.y);
      ctx.closePath();
      ctx.fillStyle = 'rgba(100,116,139,0.34)';
      ctx.strokeStyle = '#475569';
      ctx.lineWidth = 1;
      ctx.fill();
      ctx.stroke();
    }

    function drawAxes() {
      if (!document.getElementById('showAxes').checked) return;
      const o = { x_m: center.x, y_m: center.y, z_m: 0 };
      line(o, { x_m: center.x + 10, y_m: center.y, z_m: 0 }, '#ef4444', 2);
      line(o, { x_m: center.x, y_m: center.y + 10, z_m: 0 }, '#22c55e', 2);
      line(o, { x_m: center.x, y_m: center.y, z_m: 10 }, '#3b82f6', 2);
      const px = project({ x_m: center.x + 10, y_m: center.y, z_m: 0 }); ctx.fillStyle = '#ef4444'; ctx.fillText('X', px.x + 4, px.y);
      const py = project({ x_m: center.x, y_m: center.y + 10, z_m: 0 }); ctx.fillStyle = '#22c55e'; ctx.fillText('Y', py.x + 4, py.y);
      const pz = project({ x_m: center.x, y_m: center.y, z_m: 10 }); ctx.fillStyle = '#3b82f6'; ctx.fillText('Z', pz.x + 4, pz.y);
    }

    function draw() {
      ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
      ctx.clearRect(0, 0, canvas.clientWidth, canvas.clientHeight);
      ctx.lineCap = 'round';
      drawAxes();

      if (document.getElementById('showWalls').checked) {
        for (const w of model.walls || []) {
          if (levelSelect.value === 'all' || (w.z_i_m <= Number(levelSelect.value) && w.z_j_m >= Number(levelSelect.value))) drawWall(w);
        }
      }

      if (document.getElementById('showSlabs').checked) {
        const slabs = model.slabs.filter(s => visibleAtLevel(s.z_m)).sort((a,b) => slabDepth(a) - slabDepth(b));
        for (const slab of slabs) drawSlab(slab);
      }

      const selected = model.slabs.find(s => s.id === state.selectedSlab);
      if (selected && visibleAtLevel(selected.z_m)) drawTributaryRegions(selected);

      if (document.getElementById('showBeams').checked) {
        for (const e of model.elements.filter(e => e.type.startsWith('BEAM'))) {
          const a = nodes.get(e.i), b = nodes.get(e.j);
          if (a && b && visibleAtLevel(a.z_m) && visibleAtLevel(b.z_m)) line(a, b, '#2563eb', 2.2);
        }
      }

      if (document.getElementById('showColumns').checked) {
        for (const e of model.elements.filter(e => e.type === 'COLUMN')) {
          const a = nodes.get(e.i), b = nodes.get(e.j);
          const selected = levelSelect.value;
          if (a && b && (selected === 'all' || (a.z_m <= Number(selected) && b.z_m >= Number(selected)))) line(a, b, '#dc2626', 3.2);
        }
      }

      if (document.getElementById('showNodes').checked) {
        for (const n of model.nodes.filter(n => visibleAtLevel(n.z_m))) {
          const p = project(n);
          ctx.beginPath(); ctx.arc(p.x, p.y, 2.6, 0, Math.PI * 2); ctx.fillStyle = '#facc15'; ctx.fill();
        }
      }
    }

    function pointInPoly(x, y, pts) {
      let inside = false;
      for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
        const xi = pts[i].x, yi = pts[i].y, xj = pts[j].x, yj = pts[j].y;
        const intersect = ((yi > y) !== (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
        if (intersect) inside = !inside;
      }
      return inside;
    }

    function showInfo(slab) {
      const info = document.getElementById('info');
      if (!slab) { info.textContent = 'Selecciona una losa.'; return; }
      const loads = slab.tributary_loads.map(l => `<tr><td>${l.edge}</td><td>${l.beam_ids.join(',')}</td><td>${l.tributary_area_m2.toFixed(2)}</td><td>${l.distribution}</td><td>${l.w_start_kN_m.toFixed(2)}/${l.w_max_kN_m.toFixed(2)}/${l.w_end_kN_m.toFixed(2)}</td></tr>`).join('');
      info.innerHTML = `<b>Losa ${slab.id}</b><br>Z=${slab.z_m.toFixed(2)} m<br>Area=${slab.area_m2.toFixed(2)} m²<br>Dims=${slab.dimensions_m.lx.toFixed(2)} x ${slab.dimensions_m.ly.toFixed(2)} m<br>Nodos=${slab.node_ids.join(', ')}<table><thead><tr><th>Borde</th><th>Viga</th><th>Atrib</th><th>Tipo</th><th>w</th></tr></thead><tbody>${loads}</tbody></table>`;
    }

    canvas.addEventListener('mousedown', event => {
      state.dragging = true;
      state.panning = event.shiftKey;
      state.last = [event.clientX, event.clientY];
    });
    window.addEventListener('mouseup', () => { state.dragging = false; });
    canvas.addEventListener('mousemove', event => {
      if (!state.dragging) return;
      const dx = event.clientX - state.last[0], dy = event.clientY - state.last[1];
      if (state.panning) { state.panX += dx; state.panY += dy; }
      else { state.az += dx * 0.008; state.ax += dy * 0.008; state.ax = Math.max(-1.45, Math.min(0.2, state.ax)); }
      state.last = [event.clientX, event.clientY];
      draw();
    });
    canvas.addEventListener('wheel', event => {
      event.preventDefault();
      state.scale *= event.deltaY < 0 ? 1.1 : 0.9;
      state.scale = Math.max(4, Math.min(60, state.scale));
      document.getElementById('zoom').value = Math.max(4, Math.min(45, state.scale));
      draw();
    }, { passive: false });
    canvas.addEventListener('click', event => {
      const rect = canvas.getBoundingClientRect();
      const x = event.clientX - rect.left, y = event.clientY - rect.top;
      const slabs = model.slabs.filter(s => visibleAtLevel(s.z_m)).sort((a,b) => slabDepth(b) - slabDepth(a));
      const slab = slabs.find(s => pointInPoly(x, y, s.coordinates.map(project)));
      state.selectedSlab = slab ? slab.id : null;
      showInfo(slab);
      draw();
    });

    for (const id of ['level','slabOpacity','showSlabs','showBeams','showColumns','showWalls','showNodes','showLabels','showTributary','showAxes']) {
      document.getElementById(id).addEventListener('input', draw);
    }
    document.getElementById('zoom').addEventListener('input', event => { state.scale = Number(event.target.value); draw(); });
    document.getElementById('reset').addEventListener('click', () => { state.ax = -0.72; state.az = -0.58; state.scale = 16; state.panX = 0; state.panY = 20; document.getElementById('zoom').value = 16; draw(); });
    window.addEventListener('resize', resize);
    resize();
  </script>
</body>
</html>
'''


def main():
    data = json.loads(MODEL.read_text(encoding="utf-8"))
    payload = json.dumps(data, separators=(",", ":"))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(HTML_TEMPLATE.replace("__MODEL_JSON__", payload), encoding="utf-8")
    print(f"Visualizador 3D creado: {OUTPUT.name}")


if __name__ == "__main__":
    main()
