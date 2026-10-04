#!/usr/bin/env python3
"""Benchmark G8: modelos de texto contra dataset controlado. Stdlib only."""
import json, subprocess, time, urllib.request, os, sys

MODELS = ["qwen2.5:7b-instruct-q4_K_M", "llama3.1:8b-instruct-q4_K_M", "gemma2:9b-instruct-q4_K_M"]

SYSTEM = (
    "Eres un asistente de nutrición. Responde ÚNICAMENTE con un objeto JSON válido, sin texto adicional. "
    "Esquema: {\"intencion\":string,\"requiere_aclaracion\":boolean,\"alimentos\":[{\"nombre\":string,\"cantidad\":number|null,\"unidad\":string|null,\"preparacion\":string|null,\"confianza\":number,\"incertidumbres\":[string]}]}. "
    "Intención en: registrar_comida, registrar_medicion, crear_objetivo, consultar_balance, desconocida. "
    "Solo usa nombres de alimentos del catálogo conocido (huevo, arroz, pollo, tortilla, frijoles, aguacate, aceite, salsa, agua, refresco). "
    "Si el alimento no está en el catálogo o la información es insuficiente, pon requiere_aclaracion=true y describe la incertidumbre en incertidumbres. "
    "Usa null cuando no se indique. Nunca inventes cantidades ni preparaciones."
)

CASES = [
    dict(input="Comí dos huevos y dos tortillas.", expect_foods=["huevo", "tortilla"], expect_qty=[2, 2],
         expect_unit=["pieza", "pieza"], expect_prep=[None, None], expect_clarify=False),
    dict(input="Comí unos tacos.", expect_foods=["taco"], expect_clarify=True),
    dict(input="Comí huevos con aceite.", expect_foods=["huevo", "aceite"], expect_clarify=False),
    dict(input="Comí 150 gramos de arroz con pollo hervido.", expect_foods=["arroz", "pollo"],
         expect_qty=[150, None], expect_unit=["g", None], expect_prep=[None, "hervido"], expect_clarify=False),
    dict(input="Tomé un vaso de agua.", expect_foods=["agua"], expect_clarify=False),
    dict(input="Comí pizza.", expect_foods=[], expect_clarify=True),
]

def chat(model, user):
    body = json.dumps({
        "model": model, "stream": False, "format": "json",
        "options": {"temperature": 0, "num_ctx": 4096},
        "messages": [{"role": "system", "content": SYSTEM}, {"role": "user", "content": user}],
    }).encode()
    req = urllib.request.Request("http://127.0.0.1:11434/api/chat", data=body, headers={"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=300) as r:
        out = json.load(r)
    latency = time.time() - t0
    return out, latency

def norm(s):
    return (s or "").lower().strip()

def score(case, parsed):
    if parsed is None:
        return dict(json_ok=False, food=False, qty=False, unit=False, prep=False, clarify=False)
    foods = [norm(a.get("nombre")) for a in parsed.get("alimentos", []) if isinstance(a, dict)]
    exp = [norm(f) for f in case.get("expect_foods", [])]
    food_ok = all(any(e in f for f in foods) for e in exp) if exp else len(foods) == 0
    qty_ok = True
    if "expect_qty" in case:
        got = [a.get("cantidad") for a in parsed.get("alimentos", [])]
        qty_ok = all(g is None or abs((g or 0) - e) < 0.51 for g, e in zip(got + [None]*10, case["expect_qty"]) if e is not None)
    unit_ok = True
    if "expect_unit" in case:
        got = [norm(a.get("unidad")) for a in parsed.get("alimentos", [])]
        exp_u = [norm(u) for u in case["expect_unit"] if u]
        unit_ok = all(any(eu and (eu in g or g in eu) for g in got) for eu in exp_u) if exp_u else True
    prep_ok = True
    if "expect_prep" in case:
        got = [norm(a.get("preparacion")) for a in parsed.get("alimentos", [])]
        exp_p = [norm(p) for p in case["expect_prep"] if p]
        prep_ok = all(any(ep and ep in g for g in got) for ep in exp_p) if exp_p else True
    clarify_ok = bool(parsed.get("requiere_aclaracion")) == bool(case["expect_clarify"])
    return dict(json_ok=True, food=food_ok, qty=qty_ok, unit=unit_ok, prep=prep_ok, clarify=clarify_ok)

def gpu_used():
    try:
        out = subprocess.run(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"],
                             capture_output=True, text=True, timeout=10).stdout.strip()
        return int(out.split("\n")[0])
    except Exception:
        return -1

def main():
    results = []
    for model in MODELS:
        print(f"== {model} ==", flush=True)
        # warmup
        try:
            chat(model, "Hola")
        except Exception as e:
            print("WARMUP ERROR:", e); results.append(dict(model=model, error=str(e))); continue
        agg = dict(json_ok=0, food=0, qty=0, unit=0, prep=0, clarify=0, n=0, latency=0.0, tps=0.0)
        for case in CASES:
            try:
                out, latency = chat(model, case["input"])
                content = out["message"]["content"]
                try:
                    parsed = json.loads(content)
                except Exception:
                    parsed = None
                s = score(case, parsed)
                agg["n"] += 1
                for k in ["json_ok", "food", "qty", "unit", "prep", "clarify"]:
                    agg[k] += int(s[k])
                agg["latency"] += latency
                ev = out.get("eval_count", 0)
                ed = out.get("eval_duration", 0)
                if ed: agg["tps"] += ev / (ed / 1e9)
                print(f"  {case['input'][:40]:42} {s} lat={latency:.1f}s vram={gpu_used()}MiB", flush=True)
            except Exception as e:
                print("  ERROR:", e); agg["n"] += 1
        results.append(dict(model=model, **agg, vram=gpu_used()))
    os.makedirs("docs", exist_ok=True)
    with open("docs/benchmark-g8.json", "w") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(json.dumps(results, indent=2, ensure_ascii=False))

if __name__ == "__main__":
    main()
