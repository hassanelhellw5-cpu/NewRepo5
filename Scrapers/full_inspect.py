from yallakora_engine import YallaKoraEngine
from datetime import datetime
import json

engine = YallaKoraEngine()
today = datetime.now().strftime("%m/%d/%Y")

print("--- Inspecting Matches ---")
matches = engine.get_matches_by_date(today)
if matches:
    print(json.dumps(matches[0], ensure_ascii=False, indent=2))
    tour_id = matches[0].get('tournament_id')
    if tour_id:
        print(f"\n--- Inspecting Tournament Details for ID {tour_id} ---")
        details = engine.get_tournament_details(tour_id)
        print(json.dumps(details, ensure_ascii=False, indent=2))

print("\n--- Inspecting News ---")
news = engine.get_latest_news()
if news:
    print(json.dumps(news[0], ensure_ascii=False, indent=2))
