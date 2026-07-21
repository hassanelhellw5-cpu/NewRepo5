import json
import os
import sys

from live_stream_scraper import LiveStreamScraper


def main():
    scraper = LiveStreamScraper(api_base_url=os.getenv('API_DOMAIN'))
    overrides = scraper.parse_overrides()
    if not overrides:
        print('No overrides found.')
        return 1

    exit_code = 0
    for index, item in enumerate(overrides, start=1):
        name = item.get('name', f'override #{index}')
        source = item.get('source', '')
        url = item.get('url', '')
        ok, message = scraper.validate_stream_url(url, item.get('headers') or {})
        status = 'LIVE' if ok else 'OFFLINE_OR_CLIP'
        if not ok:
            exit_code = 2
        print(f'[{status}] {name} - {source}: {message}')

    return exit_code


if __name__ == '__main__':
    sys.exit(main())
