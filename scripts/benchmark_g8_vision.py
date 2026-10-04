#!/usr/bin/env python3
"""G8.10: smoke benchmark de visión con imágenes sintéticas controladas. Stdlib only."""
import base64, json, subprocess, time, urllib.request, glob, os

MODELS = ["qwen2.5vl:7b-q4_K_M", "llava:7b"]
SYSTEM = (
    "Describe la imagen como JSON válido y ÚNICAMENTE JSON. "
    'Esquema: {"alimentos":[{"nombre":string,"cantidad":number|null,"unidad":string|null,"preparacion":string|null,"confianza":number,"incertidumbres":[string]}],"intencion":string,"requiere_aclaracion":boolean}. '
    "Si no estás seguro, usa requiere_aclaracion=true."
)

def chat(model, image_b64):
    body = json.dumps({
        "model": model, "stream": False, "format": "json",
        "options": {"temperature": 0, "num_ctx": 4096},
        "messages": [{"role": "user", "content": "Analiza esta imagen de comida.", "images": [image_b64]}],
        "system": SYSTEM,
    }).encode()
    req = urllib.request.Request("http://127.0.0.1:11434/api/chat", data=body, headers={"Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=300) as r:
        out = json.load(r)
    return out, time.time() - t0

def gpu():
    try:
        return int(subprocess.run(["nvidia-smi","--query-gpu=memory.used","--format=csv,noheader,nounits"],
                    capture_output=True, text=True, timeout=10).stdout.strip().split("\n")[0])
    except Exception: return -1

results = []
for model in MODELS:
    print(f"== {model} ==")
    # warmup
    try:
        img = base64.b64encode(open(sorted(glob.glob('/tmp/opencode/img_*.png'))[0],'rb').read()).decode()
        chat(model, img)
    except Exception as e:
        print("WARMUP ERROR:", e); results.append({"model": model, "error": str(e)}); continue
    agg = {"json_ok":0,"n":0,"latency":0.0,"tps":0.0}
    for img_path in sorted(glob.glob('/tmp/opencode/img_*.png')):
        try:
            img = base64.b64encode(open(img_path,'rb').read()).decode()
            out, lat = chat(model, img)
            content = out["message"]["content"]
            try:
                json.loads(content); ok = True
            except Exception: ok = False
            agg["n"] += 1; agg["json_ok"] += int(ok); agg["latency"] += lat
            ev, ed = out.get("eval_count",0), out.get("eval_duration",0)
            if ed: agg["tps"] += ev/(ed/1e9)
            print(f"  {os.path.basename(img_path)}: json_ok={ok} lat={lat:.1f}s vram={gpu()}MiB -> {content[:90]}")
        except Exception as e:
            print("  ERROR:", e); agg["n"] += 1
    results.append({"model":model, **agg, "vram":gpu()})

os.makedirs('docs', exist_ok=True)
json.dump(results, open('docs/benchmark-g8-vision.json','w'), indent=2, ensure_ascii=False)
print(json.dumps(results, indent=2, ensure_ascii=False))
