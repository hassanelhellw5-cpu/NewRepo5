import requests
from bs4 import BeautifulSoup
import os

url = "https://www.yallakora.com/match-center"
headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
}

try:
    response = requests.get(url, headers=headers, timeout=15)
    print(f"Status Code: {response.status_code}")
    with open("debug.html", "w", encoding="utf-8") as f:
        f.write(response.text)
    
    soup = BeautifulSoup(response.content, 'html.parser')
    
    # Try different selectors
    match_cards = soup.find_all('div', class_=lambda x: x and 'matchCard' in x)
    print(f"Match cards found with lambda: {len(match_cards)}")
    
    match_cards_direct = soup.find_all('div', class_='matchCard')
    print(f"Match cards found direct: {len(match_cards_direct)}")
    
    # Check for any div with 'tour' or 'match' in class
    all_divs = soup.find_all('div')
    classes = set()
    for d in all_divs:
        cls = d.get('class')
        if cls:
            for c in cls:
                if 'match' in c.lower() or 'tour' in c.lower():
                    classes.add(c)
    print(f"Relevant classes found: {classes}")

except Exception as e:
    print(f"Error: {e}")
