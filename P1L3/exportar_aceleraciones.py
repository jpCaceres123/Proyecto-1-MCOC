"""Aceleraciones estáticas equivalentes F/m por piso y bloque; no respuesta dinámica."""
import csv
import json
import math
from pathlib import Path


def export_accelerations(source, target, gravity):
    with source.open(encoding='utf-8-sig', newline='') as stream:
        floors = list(csv.DictReader(stream))
    rows = []
    for floor in floors:
        mass, force = float(floor['masa_t']), float(floor['F_kN'])
        if not math.isfinite(mass) or mass <= 0 or not math.isfinite(force) or gravity <= 0:
            raise ValueError('Masa, fuerza o gravedad inválida para aceleración equivalente')
        acceleration = force / mass  # kN / t = m/s²
        rows.append(dict(bloque=floor['bloque'],z_m=floor['z_m'],masa_t=mass,
            fuerza_kN=force,a_m_s2=acceleration,a_g=acceleration/gravity))
    with target.open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=['bloque','z_m','masa_t','fuerza_kN','a_m_s2','a_g'])
        writer.writeheader()
        writer.writerows(rows)
    return rows


if __name__ == '__main__':
    root=Path(__file__).resolve().parent
    # Usar la gravedad de la corrida que produjo las masas, no parámetros editados después.
    manifest=json.loads((root/'results/manifest.json').read_text(encoding='utf-8'))
    cfg=manifest['parametros']
    rows=export_accelerations(root/'results/masas_y_sismo.csv',
        root.parent/'P1L2/UnityVisualization/Assets/Resources/semana3_aceleraciones.csv',cfg['g_m_s2'])
    print(f'Exportadas aceleraciones equivalentes para {len(rows)} pisos/bloques.')
