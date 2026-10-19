"""Compile unchanged services and run only isolated, bounded macOS fixtures."""
from pathlib import Path
import os
import signal
import subprocess
import tempfile

artifact_dir = Path(__file__).resolve().parent
repo_root = artifact_dir.parents[1]
sources = [
    repo_root / "apps/macos/SpacePilotMac/Sources/SpacePilotMac/Models.swift",
    repo_root / "apps/macos/SpacePilotMac/Sources/SpacePilotMac/Services.swift",
]
with tempfile.TemporaryDirectory(prefix="spacepilot-audit-build-", dir="/private/tmp") as work:
    work_dir = Path(work)
    extended = work_dir / "extended"
    subprocess.run(
        ["swiftc", "-module-cache-path", str(work_dir / "cache"),
         *map(str, sources), str(artifact_dir / "mac-extended-probe.swift"),
         "-o", str(extended)],
        check=True,
    )
    result = subprocess.run([str(extended)], check=True, text=True, capture_output=True)
    print(result.stdout, end="")

    pipe = work_dir / "pipe"
    subprocess.run(
        ["swiftc", "-parse-as-library", "-module-cache-path", str(work_dir / "cache"),
         str(artifact_dir / "mac-pipe-probe.swift"), "-o", str(pipe)],
        check=True,
    )
    data = work_dir / "controlled-output.bin"
    data.write_bytes(b"X" * (2 * 1024 * 1024))
    proc = subprocess.Popen(
        [str(pipe), str(data)], stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, start_new_session=True,
    )
    try:
        stdout, stderr = proc.communicate(timeout=3)
        print(stdout, end="")
        print(f"pipe_timeout=false; exit_code={proc.returncode}; stderr={stderr.strip()}")
    except subprocess.TimeoutExpired:
        os.killpg(proc.pid, signal.SIGKILL)
        stdout, _ = proc.communicate()
        print(stdout, end="")
        print("pipe_timeout=true; timeout_seconds=3; isolated_process_group_terminated=true")
