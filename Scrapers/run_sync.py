import subprocess
import time
import sys
import os
import shutil
from datetime import datetime

# CONFIGURATION
API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net") # Final Domain Configured


def has_chrome_binary():
    configured = os.getenv("CHROME_BINARY")
    if configured and os.path.exists(configured):
        return True
    return any(shutil.which(name) for name in ("google-chrome", "chrome", "chromium", "chromium-browser", "msedge"))

def run_script(script_name, args=None, extra_env=None):
    args = args or []
    script_path = os.path.join(os.path.dirname(__file__), script_name)
    env = os.environ.copy()
    env["API_DOMAIN"] = API_DOMAIN
    if extra_env:
        env.update(extra_env)
    cmd = [sys.executable, script_path] + args
    print(f"[{datetime.now()}] Running: {' '.join(cmd)}")
    try:
        subprocess.run(cmd, check=True, env=env)
    except Exception as e:
        print(f"Error running {script_name}: {e}")

def main():
    print(f"Starting Qemma Sync Master...")
    print(f"Target API: {API_DOMAIN}")
    
    # 1. Initial full sync through the maintained orchestrator. run_all.py now
    # covers football + other sports (ice hockey, tennis, basketball, F1 when
    # Chrome/Selenium is available) with the same appsettings/env mapping as
    # the ASP.NET background worker.
    run_script("run_all.py", extra_env={
        "RUN_ALL_PROFILE": os.getenv("RUN_ALL_PROFILE", "full"),
        "RUN_ALL_DATE_RANGE_MODE": os.getenv("RUN_ALL_DATE_RANGE_MODE", "full_sync"),
    })
    
    last_full_sync = time.time()
    full_sync_interval = int(os.getenv("FULL_SYNC_INTERVAL_SECONDS", "600")) # 10 minutes by default for news and tournament details
    
    try:
        while True:
            current_time = time.time()
            
            # Every loop: Sync Live Streams & Match Results
            # yallakora_engine.py "matches" will also trigger tournament details for active matches
            run_script("yallakora_engine.py", ["matches", "--today"])
            run_script("other_sports_importer.py")
            if os.getenv("ENABLE_SELENIUM_MULTISPORT_IMPORT", "true").strip().lower() not in {"0", "false", "no", "off"} and has_chrome_binary():
                run_script("multisport_selenium_importer.py")
            elif os.getenv("ENABLE_SELENIUM_MULTISPORT_IMPORT", "true").strip().lower() not in {"0", "false", "no", "off"}:
                print("Skipping multisport_selenium_importer.py: no Chrome/Chromium binary found.")
            run_script("live_stream_scraper.py")
            run_script("yallakora_video_scraper.py")
            
            # Every 10 minutes: Sync everything again to ensure standings/scorers/news are fresh
            if current_time - last_full_sync > full_sync_interval:
                run_script("run_all.py", extra_env={
                    "RUN_ALL_PROFILE": os.getenv("RUN_ALL_PROFILE", "full"),
                    "RUN_ALL_DATE_RANGE_MODE": os.getenv("RUN_ALL_DATE_RANGE_MODE", "full_sync"),
                })
                last_full_sync = current_time
            
            loop_interval = float(os.getenv("YALLAKORA_LOOP_SECONDS", "5"))
            print(f"Waiting {loop_interval:g} seconds for next update...")
            time.sleep(max(5, loop_interval))
            
    except KeyboardInterrupt:
        print("Sync stopped by user.")

if __name__ == "__main__":
    main()
