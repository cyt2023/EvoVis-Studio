from real_llm import call_qwen_turbo


response = call_qwen_turbo("Reply with exactly: OK", temperature=0)
print(response["text"])
