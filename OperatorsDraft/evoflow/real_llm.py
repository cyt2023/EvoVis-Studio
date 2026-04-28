import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


ROOT = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path(__file__).resolve().parent.parent
DEFAULT_MODEL = "qwen-turbo"
COMPATIBLE_ENDPOINT = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"


def load_local_env() -> None:
    for env_path in (ROOT.parent / ".env", ROOT.parent / ".env.local", ROOT / ".env", ROOT / ".env.local"):
        if not env_path.exists():
            continue

        with env_path.open("r", encoding="utf-8") as handle:
            for line in handle:
                stripped = line.strip()
                if not stripped or stripped.startswith("#") or "=" not in stripped:
                    continue

                key, value = stripped.split("=", 1)
                key = key.strip()
                value = value.strip().strip('"').strip("'")
                if key and key not in os.environ:
                    os.environ[key] = value


def _require_env(name: str) -> str:
    load_local_env()
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"Missing environment variable: {name}")

    return value


def call_qwen_turbo(user_message: str, *, model: str | None = None, temperature: float = 0.2) -> dict[str, Any]:
    api_key = _require_env("DASHSCOPE_API_KEY")
    model_name = (model or os.environ.get("DASHSCOPE_MODEL") or DEFAULT_MODEL).strip() or DEFAULT_MODEL

    body = {
        "model": model_name,
        "messages": [
            {
                "role": "system",
                "content": (
                    "You are the EvoFlow planner. Follow the user instructions exactly. "
                    "When JSON is requested, return valid JSON only with no markdown fences."
                ),
            },
            {"role": "user", "content": user_message},
        ],
        "temperature": temperature,
    }
    raw_body = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        COMPATIBLE_ENDPOINT,
        data=raw_body,
        method="POST",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            response_text = response.read().decode("utf-8", errors="replace")
            data = json.loads(response_text) if response_text.strip() else {}
    except urllib.error.HTTPError as exc:
        response_text = exc.read().decode("utf-8", errors="replace")
        try:
            data = json.loads(response_text) if response_text.strip() else {}
        except json.JSONDecodeError:
            data = {}

        print(
            "[DashScope qwen-turbo API error] "
            + json.dumps(
                {
                    "status": exc.code,
                    "request_id": data.get("request_id") or data.get("id"),
                    "code": data.get("code"),
                    "message": data.get("message") or data.get("error", {}).get("message"),
                    "raw": data if data else response_text,
                },
                ensure_ascii=False,
            ),
            flush=True,
        )
        message = data.get("message") or data.get("error", {}).get("message")
        raise RuntimeError(message or f"DashScope qwen-turbo request failed with status {exc.code}") from exc
    except urllib.error.URLError as exc:
        reason = getattr(exc, "reason", exc)
        print(f"[DashScope qwen-turbo network error] {reason}", flush=True)
        raise RuntimeError(f"DashScope qwen-turbo network error: {reason}") from exc
    except json.JSONDecodeError as exc:
        print(f"[DashScope qwen-turbo API error] Invalid JSON response: {exc}", flush=True)
        raise RuntimeError("DashScope qwen-turbo returned invalid JSON.") from exc

    choices = data.get("choices") if isinstance(data, dict) else []
    first_choice = choices[0] if isinstance(choices, list) and choices else {}
    message = first_choice.get("message") if isinstance(first_choice, dict) else {}
    if not isinstance(message, dict):
        message = {}

    return {
        "text": message.get("content") or "",
        "sessionId": None,
        "raw": data,
    }


def run_qwen_llm(model_size, prompt, temperature=0.7):
    print("[DashScope qwen-turbo] Starting compatible-mode request.", flush=True)
    try:
        started_at = time.time()
        result = call_qwen_turbo(prompt, temperature=temperature)
        elapsed = time.time() - started_at

        answer = result.get("text") or ""
        if not answer.strip():
            print(f"[DashScope qwen-turbo] Completed after {elapsed:.2f}s but returned empty text.", flush=True)
            return "ERROR"

        print(f"[DashScope qwen-turbo] Completed successfully in {elapsed:.2f}s", flush=True)
        return answer
    except Exception as exc:
        print(f"[DashScope qwen-turbo] Exception during request | {exc}", flush=True)
        return "ERROR"
