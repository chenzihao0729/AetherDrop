"""Check original-media negotiation and lossless binary storage without a radio."""
import hashlib
import io
import json
import pathlib
import plistlib
import sys
import os
import tempfile
import types

root = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, os.environ.get('AETHERDROP_TEST_ENGINE', str(root / 'build/stage/airdrop')))
import receiver

handler = receiver.Handler.__new__(receiver.Handler)
handler.server = types.SimpleNamespace(settings=types.SimpleNamespace(name='AetherDrop test', model='MacBookPro18,3'))
answer = plistlib.loads(handler.identity_plist())
capabilities = json.loads(answer['ReceiverMediaCapabilities'])
profiles = capabilities['Codecs']['hvc1']['Profiles']
assert capabilities['Version'] == 1
assert profiles['VTSupportedProfiles'] == [1, 2, 3]
assert all(profiles['VTPerProfileSupport'][str(profile)]['VTMaxPlaybackLevel'] >= 153 for profile in (1, 2))
assert 'public.heic' in capabilities['ContainerFormats']['public.heif-standard']['HeifSubtypes']
print('PASS discovery declares HEVC Main/Main10 at a level covering 4K60 and HEIF containers')

# The receiver stores an opaque video container; it has no reason to alter
# frame timing, codec metadata or compressed media bytes during extraction.
import cpio
import transfer
video_bytes = b'\0\0\0\x14ftypqt  ' + bytes(range(256)) * 4096

def member(name, content=b''):
    pathname = name.encode() + b'\0'
    header = '070707' + '000000' * 2 + f'{0o100644:06o}' + '000000' * 4
    header += f'{0:011o}{len(pathname):06o}{len(content):011o}'
    return header.encode() + pathname + content

archive = member('original.MOV', video_bytes) + member(cpio.TRAILER)
body = (len(archive) | 0x80000000).to_bytes(4, 'big') + archive
with tempfile.TemporaryDirectory(prefix='AetherDrop-original-media-') as destination:
    names = transfer.store_upload(io.BytesIO(body), destination, 'original-media-test',
                                  types.SimpleNamespace(byte_limit=16 * 1024 * 1024))
    stored = pathlib.Path(destination, names[0]).read_bytes()
    assert hashlib.sha256(stored).digest() == hashlib.sha256(video_bytes).digest()
print('PASS MOV payload is stored byte-for-byte with an identical SHA-256')
