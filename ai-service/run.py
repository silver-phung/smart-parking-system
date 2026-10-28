import os
import sys
import subprocess

# Auto-detect and switch to .venv python if running outside virtual environment
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
VENV_PYTHON = os.path.join(CURRENT_DIR, ".venv", "Scripts", "python.exe")

if os.path.exists(VENV_PYTHON) and sys.executable.lower() != os.path.abspath(VENV_PYTHON).lower():
    sys.exit(subprocess.call([VENV_PYTHON, os.path.abspath(__file__)] + sys.argv[1:]))

import uvicorn

if __name__ == "__main__":
    uvicorn.run("app.main:app", host="0.0.0.0", port=8000, reload=True)
