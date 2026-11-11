#!/bin/sh
# Run on the target Mac with Python 3.11 and Unity macOS build support installed.
# PyInstaller must run natively; Windows cannot produce a verified macOS worker.
set -eu
cd "$(dirname "$0")/../.."
python3.11 -m venv Tools/Recognition/mac-env
Tools/Recognition/mac-env/bin/pip install -r Tools/Recognition/requirements-macos.txt pyinstaller==6.11.1
Tools/Recognition/mac-env/bin/pyinstaller --noconfirm --onedir --name neonflow-recognition --collect-all mediapipe --collect-all vosk --collect-all sounddevice --distpath Tools/Recognition/mac-dist --workpath Tools/Recognition/mac-work Assets/StreamingAssets/Recognition/worker.py
mkdir -p Assets/StreamingAssets/Recognition/mac
cp -R Tools/Recognition/mac-dist/neonflow-recognition/. Assets/StreamingAssets/Recognition/mac/
# Models are resolved relative to the outer StreamingAssets/Recognition by worker.
