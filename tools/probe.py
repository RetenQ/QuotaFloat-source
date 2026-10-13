import subprocess, os, json, threading, queue, time, shutil
from pathlib import Path

q = queue.Queue()
env = os.environ.copy()
env['HOME'] = env.get('USERPROFILE') or os.path.expanduser('~')
codex = shutil.which('codex')
if not codex:
    root = Path(os.environ.get('LOCALAPPDATA', '')) / 'OpenAI' / 'Codex' / 'bin'
    candidates = list(root.glob('**/codex.exe')) if root.exists() else []
    if candidates: codex = str(max(candidates, key=lambda path: path.stat().st_mtime))
if not codex: raise SystemExit('Codex executable not found; install Codex or add codex.exe to PATH.')
p = subprocess.Popen([codex, 'app-server', '--stdio'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8', env=env)
def reader():
    for line in p.stdout:
        try: q.put(json.loads(line))
        except Exception: pass
threading.Thread(target=reader, daemon=True).start()
threading.Thread(target=lambda: [None for _ in p.stderr], daemon=True).start()
def call(i, method, params=None):
    p.stdin.write(json.dumps(dict(id=i, method=method, params=params))+'\n'); p.stdin.flush()
    end = time.time()+40
    while time.time()<end:
        x=q.get(timeout=max(0.1,end-time.time()))
        if x.get('id')==i:
            if 'error' in x: return {'errorCode':x['error'].get('code'), 'errorMessage':x['error'].get('message')}
            return x.get('result')
try:
    init=call(1,'initialize',{'clientInfo':{'name':'quota_float_probe','version':'0.1.0'},'capabilities':{'experimentalApi':True}})
    print('initialize', json.dumps({'ok': 'errorCode' not in init, 'fields': list(init.keys())},ensure_ascii=False), flush=True)
    p.stdin.write('{"method":"initialized"}\n'); p.stdin.flush()
    a=call(2,'account/read',{'refreshToken':False})
    acc=a.get('account') or {}
    print('account',json.dumps({'fields':list(acc.keys()),'type':acc.get('type'),'plan':acc.get('planType'),'error':a.get('errorMessage')},ensure_ascii=False),flush=True)
    r=call(3,'account/rateLimits/read')
    safe={k:v for k,v in r.items() if k not in ['accountId']}
    print('limits',json.dumps(safe,ensure_ascii=False),flush=True)
    u=call(4,'account/usage/read')
    print('usage',json.dumps({'keys':list(u.keys()),'summary':u.get('summary'),'dailyCount':len(u.get('dailyUsageBuckets') or []),'dailySample':(u.get('dailyUsageBuckets') or [])[-2:],'error':u.get('errorMessage')},ensure_ascii=False),flush=True)
finally:
    p.stdin.close()
    try:p.wait(timeout=5)
    except subprocess.TimeoutExpired:p.kill();p.wait()
