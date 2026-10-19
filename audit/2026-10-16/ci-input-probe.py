"""Harmless local reproduction of release.yml input interpolation.

Only prints a fixed marker. Does not contact GitHub or access credentials.
"""
import subprocess

version_input = "0.1.0$(printf audit-code-executed)"
rendered_script = 'version="' + version_input + '"\nprintf "%s\\n" "$version"\n'
result = subprocess.run(["/bin/bash", "-c", rendered_script], check=True, capture_output=True, text=True)
print("literal_input=" + version_input)
print("runner_result=" + result.stdout.strip())
assert result.stdout.strip() == "0.1.0audit-code-executed"
print("workflow_input_interpreted_as_code=true")
