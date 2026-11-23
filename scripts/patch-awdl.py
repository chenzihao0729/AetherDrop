# Applies nekodrop's changes to winawdl's awdld sources before they are built.
# Usage: python patch-awdl.py <winawdl folder>
#
# - awdl0 is retried when Wintun says it already exists (#30).  The service
#   restarts awdld whenever the radio restarts (a channel move, a Wi-Fi join),
#   and the old awdld is killed, so its Wintun adapter (same fixed GUID) can
#   still be on its way out when the new one asks for it.  CreateAdapter then
#   fails with ERROR_ALREADY_EXISTS (183) and awdld runs on without a host
#   interface: every AWDL data frame is dropped, so the iPhone sees the PC but
#   no AirDrop gets through until the next restart.  Waiting for the old one
#   to go fixes it; other errors fail at once, as before.
#
# Idempotent; exits with an error when the source no longer matches.
import pathlib
import sys

host_if = pathlib.Path(sys.argv[1]) / 'awdl' / 'win' / 'host_if.c'
text = host_if.read_text(encoding='utf-8')

old = '\thost->adapter = host->api.CreateAdapter(ADAPTER_NAME, TUNNEL_TYPE, &adapter_guid);\n'
new = '\n'.join([
    '\t/* nekodrop (#30): the previous awdld\'s adapter may still be going away. */',
    '\tfor (int tries = 0;; tries++) {',
    '\t\thost->adapter = host->api.CreateAdapter(ADAPTER_NAME, TUNNEL_TYPE, &adapter_guid);',
    '\t\tif (host->adapter || GetLastError() != ERROR_ALREADY_EXISTS || tries >= 40)',
    '\t\t\tbreak;',
    '\t\tSleep(250);',
    '\t}',
    '',
])
marker = "nekodrop (#30)"

if marker in text:
    print('host_if.c: already patched')
elif text.count(old) == 1:
    host_if.write_text(text.replace(old, new), encoding='utf-8')
    print('host_if.c: awdl0 retried while the old adapter goes away')
else:
    sys.exit('host_if.c: the CreateAdapter line moved; update patch-awdl.py')
