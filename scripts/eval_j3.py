#!/usr/bin/env python3
"""J3: eval end-to-end contra Api real + PostgreSQL + Ollama (Qwen).

Clasifica cada caso en: correcto, aclaracion_correcta, falso_positivo,
aclaracion_innecesaria, rechazo_correcto, error. Persiste un reporte en docs/.
Uso: python3 scripts/eval_j3.py [--api http://127.0.0.1:5080]
Requiere Api corriendo (dotnet run --project src/Nutriologo.Api --urls http://127.0.0.1:5080)
y Ollama con el modelo configurado en appsettings.json.
"""
import json, subprocess, sys, time, urllib.request

API = 'http://127.0.0.1:5080'
DATASET = 'scripts/eval_j3_dataset.json'

def post(path, body, token=None):
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = f'Bearer {token}'
    req = urllib.request.Request(API + path, data=json.dumps(body).encode(), headers=headers)
    t0 = time.time()
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            return json.load(r), time.time() - t0
    except urllib.error.HTTPError as e:
        return {'error': e.read().decode()[:200]}, time.time() - t0

def classify(expected, tipo):
    if tipo == 'registrado':
        if expected in ('registrado', 'registrado_o_aclaracion'):
            return 'correcto'
        return 'falso_positivo'
    if tipo == 'aclaracion':
        if expected == 'aclaracion':
            return 'aclaracion_correcta'
        if expected == 'registrado':
            return 'aclaracion_innecesaria'
        return 'aclaracion_correcta' if expected == 'registrado_o_aclaracion' else 'rechazo_correcto'
    return 'error'

# Limpieza: registrar el máx existente para borrar ids creados por el eval después
def max_registro_id():
    try:
        cmd = ['docker', 'exec', 'nutriologo-db', 'psql', '-U', 'postgres', '-d', 'nutriologo', '-t',
               '-c', 'SELECT COALESCE(MAX("Id"),0) FROM "RegistroComida"']
        out = subprocess.run(['sudo', '-S'] + cmd, stdin=subprocess.DEVNULL, capture_output=True, text=True).stdout
        return int(out.strip().split('\n')[-1])
    except Exception:
        return None

def main():
    max_before = max_registro_id()
    # Auth: usuario efímero para el eval (requiere Api con Auth__Key).
    import uuid as _uuid
    correo = f"eval_{_uuid.uuid4().hex[:8]}@test.local"
    reg, _ = post('/api/auth/registro', {'nombre': 'Eval', 'correo': correo, 'password': 'Segura1234'})
    token = reg.get('token')
    if not token:
        print('ERROR: no se obtuvo token de /api/auth/registro:', reg)
        sys.exit(2)
    cases = json.load(open(DATASET))
    counts = {}
    latencies = []
    rows = []
    for c in cases:
        out, lat = post('/api/conversacion/mensaje', {'texto': c['input']}, token)
        tipo = out.get('tipo', 'error') if isinstance(out, dict) and 'tipo' in out else 'error'
        resultado = classify(c['expected'], tipo)
        counts[resultado] = counts.get(resultado, 0) + 1
        latencies.append(lat)
        rows.append((c['input'], c['expected'], tipo, resultado, round(lat, 2)))
        print(f"{resultado:28} lat={lat:5.1f}s  '{c['input']}' -> {tipo}")

    # Limpiar registros creados por el eval (requiere docker con sudo + password en EVAL_SUDO_PW)
    import os as _os
    _pw = _os.environ.get('EVAL_SUDO_PW', '')
    if max_before is not None and _pw:
        p = subprocess.run(['sudo', '-S', 'docker', 'exec', 'nutriologo-db', 'psql', '-U', 'postgres', '-d', 'nutriologo',
                        '-c', f'DELETE FROM "AlimentoRegistroComida" WHERE "IdRegistroComida" > {max_before}; DELETE FROM "RegistroComida" WHERE "Id" > {max_before};'],
                       input=_pw, capture_output=True, text=True)
        if p.returncode != 0: print('AVISO: cleanup no ejecutado:', p.stderr[:200])
    elif max_before is not None:
        print('AVISO: EVAL_SUDO_PW no definido; registros del eval pueden quedar en la DB.')

    total = len(cases)
    report = [
        '# J3 — Evaluación end-to-end (Api + PostgreSQL + Ollama)',
        '',
        f'- Casos: {total}',
        f'- Clasificación: {json.dumps(counts, ensure_ascii=False, indent=2)}',
        f'- Latencia media: {sum(latencies)/len(latencies):.2f}s (max {max(latencies):.2f}s)',
        '',
        '| Entrada | Esperado | Obtenido | Clasificación | Latencia |',
        '|---|---|---|---|---|',
    ]
    for inp, exp, tipo, res, lat in rows:
        report.append(f"| `{inp}` | {exp} | {tipo} | {res} | {lat}s |")
    report.append('')
    report.append('Clasificación: correcto / aclaracion_correcta / falso_positivo / aclaracion_innecesaria / rechazo_correcto / error.')
    report.append('El loose end PIEZA→g se mantiene como variable conocida, no se contaminó el dataset.')

    import os
    os.makedirs('docs', exist_ok=True)
    open('docs/eval-j3.md', 'w').write('\n'.join(report) + '\n')
    print('\nReporte en docs/eval-j3.md')
    print('Clasificación:', counts)

if __name__ == '__main__':
    main()
