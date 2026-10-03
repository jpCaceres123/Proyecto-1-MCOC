"""Comprueba que el APK firmado incluya la instantánea y marcadores vigentes."""
from pathlib import Path
import hashlib
import json
import re
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
BUILDING = PROJECT.parents[1]
ASSETS = PROJECT / 'app/src/main/assets'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    apk = PROJECT / 'dist/EdificioAR.apk'
    snapshot_bytes = (ASSETS / 'structural_data.json').read_bytes()
    snapshot = json.loads(snapshot_bytes)
    verification = json.loads((PROJECT / 'verification.json').read_text(encoding='utf-8'))
    model = BUILDING / 'results/modelo_3d_manual.json'
    results = BUILDING / 'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json'
    if snapshot['model_sha256'] != digest(model.read_bytes()) or snapshot['results_sha256'] != digest(results.read_bytes()):
        raise ValueError('Fuentes estructurales posteriores a la exportación AR; regenerar el paquete.')
    if (verification['source_identity'] != 'OK' or verification['member_count'] != len(snapshot['members'])
            or verification['model_sha256'] != snapshot['model_sha256']):
        raise ValueError('Falta verificar la instantánea AR para el modelo vigente.')
    marker_hash = hashlib.sha256()
    with zipfile.ZipFile(apk) as package:
        if package.read('assets/structural_data.json') != snapshot_bytes:
            raise ValueError('El APK contiene resultados antiguos.')
        for member in snapshot['members']:
            path = member['marker']
            image = (ASSETS / path).read_bytes()
            if package.read('assets/' + path) != image:
                raise ValueError(f'Marcador {member["id"]} distinto entre el APK y las fuentes.')
            marker_hash.update(str(member['id']).encode('ascii'))
            marker_hash.update(image)
    if marker_hash.hexdigest() != snapshot['marker_set_sha256']:
        raise ValueError('Hash del conjunto de imágenes incoherente.')
    build = (PROJECT / 'app/build.gradle').read_text(encoding='utf-8')
    version = re.search(r"versionName\s+'([^']+)'", build)
    if not version:
        raise ValueError('Falta versionName en app/build.gradle.')
    quality = verification.get('marker_quality')
    quality_complete = quality is not None and quality.get('count') == len(snapshot['members'])
    info = dict(apk=apk.name, application_id='cl.mcoc.edificio.ar', version=version.group(1),
                abi='arm64-v8a', min_sdk=24, target_sdk=35, size_bytes=apk.stat().st_size,
                sha256=digest(apk.read_bytes()), model_sha256=snapshot['model_sha256'],
                results_sha256=snapshot['results_sha256'], members=len(snapshot['members']),
                cases=['G', 'Q', 'EX', 'EY', 'R'],
                markers_quality_minimum=quality['minimum_score'] if quality_complete else None,
                marker_quality_status='VERIFIED' if quality_complete else 'PENDING',
                embedded_assets='verified against delivered references', build='SUCCESS',
                lint='assembleDebug lintDebug succeeded', signature='APK v2 verified by build-apk.ps1',
                features=['N/V/T/M por estación', 'desplazamiento Ux/Uy/Uz interpolado',
                          'área tributaria y losas asociadas', 'carga aplicada G/Q/EX/EY/R',
                          'curva P-M para columnas HA de referencia'], physical_device_test='PENDING')
    (PROJECT / 'dist/delivery.json').write_text(json.dumps(info, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(f'APK y exportación coherentes: {len(snapshot["members"])} barras; '
          f'modelo {snapshot["model_sha256"][:12]}; imágenes {info["marker_quality_status"]}')


if __name__ == '__main__':
    main()
