from yallakora_engine import YallaKoraEngine
from datetime import datetime
import json

engine = YallaKoraEngine()
today = datetime.now().strftime("%m/%d/%Y")
matches = engine.get_matches_by_date(today)
news = engine.get_latest_news()

print("--- Matches Found ---")
print(json.dumps(matches[:1], ensure_ascii=False, indent=2))
print(f"Total Tournaments: {len(matches)}")

print("\n--- News Found ---")
print(json.dumps(news[:1], ensure_ascii=False, indent=2))
print(f"Total News Items: {len(news)}")
