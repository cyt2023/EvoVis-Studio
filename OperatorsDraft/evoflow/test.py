import dashscope
from dashscope import Generation
import os

api_key = os.environ.get("DASHSCOPE_API_KEY", "").strip()
if not api_key:
    raise SystemExit("DASHSCOPE_API_KEY is not set.")

response = Generation.call(model=Generation.Models.qwen_turbo, api_key=api_key, prompt="测试")
print(response)
