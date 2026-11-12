"""Regression for Compose consuming the remainder of the streamed SSH script.
Uses disposable command shims; does not connect to a VPS or run containers.
"""
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap

workflow = Path('.github/workflows/deploy.yml').read_text()
script = textwrap.dedent(workflow.split("<<'REMOTE'\n", 1)[1].split('\n          REMOTE', 1)[0])
revision = 'b' * 40
with tempfile.TemporaryDirectory() as temp:
    root = Path(temp)
    checkout = root / 'checkout'
    checkout.mkdir()
    (checkout / '.env').write_text('')
    commands = root / 'commands'
    commands.mkdir()
    events = root / 'events'
    for name, code in {
        'git': "pass",
        'docker': """
with open(os.environ['FHGM_TEST_EVENTS'], 'a') as log:
    log.write('docker ' + ' '.join(sys.argv[1:]) + '\\n')
if 'run' in sys.argv:
    # Compose defaults to interactive stdin; consume anything it inherits.
    sys.stdin.buffer.read()
""",
        'curl': """
url = sys.argv[-1]
with open(os.environ['FHGM_TEST_EVENTS'], 'a') as log:
    log.write(url + '\\n')
if url.endswith('/health/version'):
    print(json.dumps({'revision':'stale-release' if os.environ.get('FHGM_TEST_BAD_RELEASE') else os.environ['FHGM_RELEASE_SHA']},separators=(',',':')))
elif url.endswith('/admin/gallery.js'):
    print('// optional-album-previews-v2')
else:
    print('Healthy')
""",
    }.items():
        executable = commands / name
        executable.write_text('#!/usr/bin/env python3\nimport os,sys,json\n' + textwrap.dedent(code))
        executable.chmod(0o755)
    env = {**os.environ, 'PATH':str(commands) + os.pathsep + os.environ['PATH'], 'FHGM_TEST_EVENTS':str(events)}

    def run(body, success=True):
        events.write_text('')
        result = subprocess.run(['bash','-s','--',str(checkout),revision], input=body, text=True, capture_output=True, env=env, timeout=10)
        assert (result.returncode == 0) == success, result.stderr
        return result.stdout, events.read_text()

    # Reproduce the old false-success behavior before asserting the fix.
    output, calls = run(script.replace(' </dev/null', ''))
    assert 'FHGM_DEPLOY_VERIFIED=' not in output
    assert '/health/version' not in calls
    assert not any(line.endswith(' up -d') for line in calls.splitlines())

    output, calls = run(script)
    assert any(line.endswith(' up -d') for line in calls.splitlines()), calls
    assert 'http://127.0.0.1:5081/health/ready' in calls
    assert 'https://admin.hisgraceministries.co.za/health/version' in calls
    assert 'https://admin.hisgraceministries.co.za/admin/gallery.js' in calls
    assert 'FHGM_DEPLOY_VERIFIED=' + revision in output
    env['FHGM_TEST_BAD_RELEASE'] = '1'
    output, calls = run(script, success=False)
    assert 'FHGM_DEPLOY_VERIFIED=' not in output
print('PASS: reproduced old false-success; fixed streamed deployment restarts frontend and verifies public release and gallery assets.')
