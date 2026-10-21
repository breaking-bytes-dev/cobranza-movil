"""Cliente mínimo de la Web API de Dataverse para la Fase 1 (SP de pp_secrets.env)."""
import json, os, urllib.request, urllib.parse

_env = {}
for l in open(os.environ.get('PP_SECRETS', os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'pp_secrets.env')), encoding='utf-8').read().replace('\r', '').split('\n'):
    if '=' in l and not l.startswith('#'):
        k, v = l.split('=', 1); _env[k.strip()] = v.strip().strip('"')
U = _env['DATAVERSE_URL'].rstrip('/'); A = U + '/api/data/v9.2/'

def token(scope=None):
    d = urllib.parse.urlencode({'client_id': _env['PP_CLIENT_ID'], 'client_secret': _env['PP_CLIENT_SECRET'],
                                'grant_type': 'client_credentials', 'scope': scope or U + '/.default'}).encode()
    return json.load(urllib.request.urlopen(urllib.request.Request(
        f"https://login.microsoftonline.com/{_env['PP_TENANT_ID']}/oauth2/v2.0/token", data=d)))['access_token']

T = token()

def rq(m, path, body=None, headers=None, raw=False, _reintento=True):
    global T
    url = path if path.startswith('http') else A + urllib.parse.quote(path, safe="/?&=$(),@'.:-_")
    h = {'Authorization': 'Bearer ' + T, 'Accept': 'application/json', 'Content-Type': 'application/json',
         'OData-MaxVersion': '4.0', 'OData-Version': '4.0', **(headers or {})}
    r = urllib.request.Request(url, method=m, data=json.dumps(body).encode() if body is not None else None, headers=h)
    try:
        with urllib.request.urlopen(r) as x:
            s = x.read(); return x.status, (json.loads(s) if s and not raw else s)
    except urllib.error.HTTPError as e:
        # El token dura ~1 hora: una corrida larga (p. ej. paso3 con miles de asignaciones) lo renueva y reintenta una vez.
        if e.code == 401 and _reintento:
            T = token()
            return rq(m, path, body, headers, raw, _reintento=False)
        return e.code, e.read().decode()[:600]

def get(path):
    st, b = rq('GET', path)
    if st >= 300: raise RuntimeError(f'GET {path[:100]} → {st}: {b}')
    return b
