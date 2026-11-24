# Applies AetherDrop's changes to the staged copy of winawdl's AirDrop engine.
# Usage: python patch-engine.py <staged airdrop folder>
#
# - A stable mDNS instance name (AETHERDROP_SERVICE_ID, kept by the client), so
#   an engine restart (a new awdl0 after the radio restarts) shows up on the
#   iPhone as the same device instead of a second one.
# - Spare receiver ports: when 8771 is held by another program the receiver
#   takes the next free one of 8772-8779 (advertised in the mDNS SRV record,
#   so the iPhone follows), instead of waiting for 8771 forever.
# - Sender presents no TLS client certificate.  An iPhone rejects a self-signed
#   one with SSLV3_ALERT_BAD_CERTIFICATE on both /Discover and /Ask; AirDrop
#   receivers accept connections without a client cert, and identity rides in
#   the plist record instead.  Without this the send direction cannot work.
import pathlib
import sys

NL = chr(10)
HELPER = NL.join([
    'SPARE_PORTS = range(8771, 8780)  # AetherDrop: the firewall rule allows these',
    '',
    '',
    'def _bind_spare(settings, watcher):',
    '    ports = [settings.port] + [p for p in SPARE_PORTS if p != settings.port]',
    '    for port in ports:',
    '        try:',
    '            return receiver.Receiver(port, settings, watcher)',
    '        except OSError as error:',
    '            if error.errno != errno.EADDRINUSE or port == ports[-1]:',
    '                raise',
    "            logging.warning('port %d held, trying the next', port)",
    '',
    '',
    '',
])

engine = pathlib.Path(sys.argv[1]) / 'engine.py'
text = engine.read_text(encoding='utf-8')
changed = False

negotiation_hook = NL.join([
    '    def connection_opened(self, address):',
    '        # Protect TLS/metadata negotiation before an /Ask exists.',
    "        self.events.say('negotiating', peer=address)",
    '',
])
if negotiation_hook not in text:
    target = '    def _open(self, sender_name, model, files, links, waits):'
    if text.count(target) != 1:
        sys.exit('engine.py: Incoming._open moved; update negotiation patch')
    text = text.replace(target, negotiation_hook + NL + target, 1)
    changed = True

# A phone can prepare/transcode a large video after accepting /Ask. Thirty
# seconds was too short and turned the accepted session into "nothing arrived".
old_due = 'UPLOAD_DUE_SECONDS = 30' + NL
new_due = 'UPLOAD_DUE_SECONDS = 180  # AetherDrop: allow large-video preparation' + NL
if new_due not in text:
    if text.count(old_due) != 1:
        sys.exit('engine.py: the upload preparation deadline moved; update patch-engine.py')
    text = text.replace(old_due, new_due, 1)
    changed = True

old = 'service_id = secrets.token_hex(6)'
new = "service_id = os.environ.get('AETHERDROP_SERVICE_ID') or secrets.token_hex(6)"
if new not in text:
    if old not in text:
        sys.exit('engine.py: the service id line moved; update patch-engine.py')
    text = text.replace(old, new, 1)
    if NL + 'import os' + NL not in text:
        text = text.replace(NL + 'import secrets' + NL, NL + 'import os' + NL + 'import secrets' + NL, 1)
    changed = True

old = 'server = receiver.Receiver(self.settings.port, self.settings, self.watcher)'
new = 'server = _bind_spare(self.settings, self.watcher)'
if new not in text:
    if old not in text:
        sys.exit('engine.py: the receiver bind line moved; update patch-engine.py')
    text = text.replace(old, new, 1)
    text = text.replace(NL + 'class ', NL + HELPER + 'class ', 1)
    changed = True

# #45: the --outdir default called valid_dir('~/Downloads') while the parser was
# built, so with no Downloads folder in the profile (deleted after choosing
# another receive folder) the engine died at start every few seconds, even
# though the client always passes --outdir.  A string default goes through
# `type` only when --outdir is missing, so standalone runs behave as before.
old = "default=receiver.valid_dir('~/Downloads'))"
new = "default='~/Downloads')  # AetherDrop: checked only when --outdir is missing (#45)"
if new not in text:
    if text.count(old) != 1:
        sys.exit('engine.py: the --outdir default moved; update patch-engine.py')
    text = text.replace(old, new, 1)
    changed = True

if changed:
    engine.write_text(text, encoding='utf-8')
print('engine patched')

# sender.py: drop the client certificate from the TLS context.  The receiver
# rejects our self-signed cert; a sender without an Apple-issued identity is
# expected to present none at all.
sender = pathlib.Path(sys.argv[1]) / 'sender.py'
text = sender.read_text(encoding='utf-8')
old = NL.join([
    '    certificate, key = identity.ensure_self_signed(keys_dir)',
    '    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)',
    '    # A receiver visible to everyone presents a certificate nothing vouches',
    '    # for, and takes ours the same way.',
    '    context.check_hostname = False',
    '    context.verify_mode = ssl.CERT_NONE',
    '    context.load_cert_chain(certificate, key)',
])
new = NL.join([
    '    # Present no client certificate.  An iPhone in Everyone mode rejects a',
    '    # self-signed one with SSLV3_ALERT_BAD_CERTIFICATE on every connection.',
    '    # AirDrop receivers accept a connection with no client cert; identity',
    '    # is carried in the plist record, not in the TLS client certificate.',
    '    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)',
    '    context.check_hostname = False',
    '    context.verify_mode = ssl.CERT_NONE',
])
if new not in text:
    if old not in text:
        sys.exit('sender.py: the client_context body moved; update patch-engine.py')
    text = text.replace(old, new, 1)
    sender.write_text(text, encoding='utf-8')
    print('sender patched')

# engine.py: follow a receiver that announces itself again.  After a transfer
# in either direction an iPhone withdraws its AirDrop service and comes back
# under a new instance (#4: peer_gone, then WinError 10061 on its old port,
# then every retry failed at once with "gone").  A send to a peer that is gone,
# or refused on its old port, now waits up to FOLLOW_SECONDS for a receiver
# with the same name and sends there.
FOLLOW = [
    (
        '        self._shy_said = 0' + NL,
        '        self._shy_said = 0' + NL +
        '        self._gone_names = {}  # AetherDrop: key -> name of receivers that went away' + NL,
    ),
    (
        NL.join([
            '            for key in [key for key in self._peers if key not in located]:',
            '                if self._peers.pop(key).name:',
            '                    gone.append(key)',
        ]),
        NL.join([
            '            for key in [key for key in self._peers if key not in located]:',
            '                old = self._peers.pop(key)',
            '                if old.name:',
            '                    self._gone_names[key] = old.name',
            '                    gone.append(key)',
        ]),
    ),
    (
        '    def link(self, scope):' + NL,
        NL.join([
            '    def find(self, key, wait):',
            '        """AetherDrop: the receiver under `key`, or the one that came back with',
            '        its name, waiting up to `wait` seconds for it."""',
            '        deadline = time.monotonic() + wait',
            '        while True:',
            '            with self._lock:',
            '                peer = self._peers.get(key)',
            '                if peer and peer.name:',
            '                    return peer',
            '                name = self._gone_names.get(key)',
            '                if name:',
            '                    for other in self._peers.values():',
            '                        if other.name == name:',
            '                            return other',
            '            if time.monotonic() >= deadline or not self.wanted.is_set():',
            '                return None',
            '            time.sleep(0.5)',
            '',
            '    def went_away(self, key):',
            '        with self._lock:',
            '            return key in self._gone_names',
            '',
            '    def link(self, scope):',
            '',
        ]),
    ),
    (
        NL.join([
            '        peer = self.looking.peer(peer_key)',
            '        if not peer or scope is None:',
            "            failed('gone')",
            '            return',
        ]),
        NL.join([
            '        peer = self.looking.peer(peer_key)',
            '        # None for a receiver that went away: looked for again in _run.',
            '        if (not peer and not self.looking.went_away(peer_key)) or scope is None:',
            "            failed('gone')",
            '            return',
        ]),
    ),
    (
        NL.join([
            '        watch = SendWatch(self.events, key, sender.archive_bytes(files))',
            '        with self._lock:',
        ]),
        NL.join([
            '        watch = SendWatch(self.events, key, sender.archive_bytes(files))',
            '        watch.peer_key = peer_key',
            '        with self._lock:',
        ]),
    ),
    (
        NL.join([
            "        watch.state('asking')",
            '        try:',
            '            outcome = sender.deliver(peer.address, peer.port, scope, self.context,',
            '                                     self.settings.name, files, watch)',
        ]),
        NL.join([
            "        watch.state('asking')",
            '        try:',
            '            if peer is None:',
            '                peer = self.looking.find(watch.peer_key, FOLLOW_SECONDS)',
            '                if peer is None:',
            "                    watch.state('failed', reason='gone')",
            '                    return',
            '            try:',
            '                outcome = sender.deliver(peer.address, peer.port, scope, self.context,',
            '                                         self.settings.name, files, watch)',
            '            except (ConnectionRefusedError, TimeoutError):',
            '                # Refused or silent before answering: the receiver may be',
            '                # announcing itself again on another port.',
            '                if watch.reached or watch.cancelled:',
            '                    raise',
            '                fresh = self.looking.find(watch.peer_key, FOLLOW_SECONDS)',
            '                if fresh is None or (fresh.address, fresh.port) == (peer.address, peer.port):',
            '                    raise',
            "                logging.info('receiver came back on port %d; sending again', fresh.port)",
            '                peer = fresh',
            '                outcome = sender.deliver(peer.address, peer.port, scope, self.context,',
            '                                         self.settings.name, files, watch)',
        ]),
    ),
    (
        'BIND_RETRY_SECONDS = 5' + NL,
        'BIND_RETRY_SECONDS = 5' + NL +
        'FOLLOW_SECONDS = 15  # AetherDrop: wait for a receiver that announces itself again' + NL,
    ),
]
engine = pathlib.Path(sys.argv[1]) / 'engine.py'
text = engine.read_text(encoding='utf-8')
changed = False
for old, new in FOLLOW:
    if new in text:
        continue
    if text.count(old) != 1:
        sys.exit('engine.py: a line the receiver-follow patch needs moved; update patch-engine.py')
    text = text.replace(old, new, 1)
    changed = True
if changed:
    engine.write_text(text, encoding='utf-8')
    print('engine: follows receivers that come back')

# transfer.py: throughput improvements for all cards (Intel, Realtek, ...):
# - 64 KiB DVZip blocks (4x fewer block headers / TLS records, matches AirDrop spec).
# - Adaptive incompressible-stream bypass: after 4 consecutive blocks do not shrink
#   under zlib level 1 (photos, HEIC, MP4, MOV, ZIP), emit DVZIP_STORED directly for
#   the next 32 blocks (2 MiB) before re-probing one block, cutting sender CPU ~90%
#   on media while still compressing text/mixed archives.
# - 0.5 s end-marker wait (down from 2 s) for iPhone videos/Live Photos that omit the
#   final chunked 0-length terminator.
transfer_path = pathlib.Path(sys.argv[1]) / 'transfer.py'
text = transfer_path.read_text(encoding='utf-8')
TRANSFER_PATCHES = [
    ('END_MARKER_WAIT_SECONDS = 2' + NL, 'END_MARKER_WAIT_SECONDS = 0.5  # AetherDrop: faster video/Live Photo completion' + NL),
    ('DVZIP_BLOCK_BYTES = 16 * KIB' + NL, 'DVZIP_BLOCK_BYTES = 64 * KIB  # AetherDrop: 64 KiB DVZip blocks' + NL),
    (
        NL.join([
            'def encode_dvzip(pieces):',
            '    """Yield the stream in `pieces` as DVZip blocks, each compressed only if',
            '    that makes it smaller."""',
            '    held = bytearray()',
            '    for piece in pieces:',
            '        held += piece',
            '        while len(held) >= DVZIP_BLOCK_BYTES:',
            '            yield _dvzip_block(bytes(held[:DVZIP_BLOCK_BYTES]))',
            '            del held[:DVZIP_BLOCK_BYTES]',
            '    if held:',
            '        yield _dvzip_block(bytes(held))',
        ]),
        NL.join([
            'def _dvzip_stored(data):',
            '    return (len(data) | DVZIP_STORED).to_bytes(4, \'big\') + data',
            '',
            '',
            'def encode_dvzip(pieces):',
            '    """Yield the stream in `pieces` as DVZip blocks, each compressed only if',
            '    that makes it smaller. Skips zlib on sustained incompressible runs (e.g.',
            '    photos/videos) and re-probes periodically so mixed archives still shrink."""',
            '    held = bytearray()',
            '    pos = 0',
            '    incomp_streak = 0',
            '    skip_left = 0',
            '    for piece in pieces:',
            '        held += piece',
            '        while len(held) - pos >= DVZIP_BLOCK_BYTES:',
            '            block = bytes(memoryview(held)[pos:pos + DVZIP_BLOCK_BYTES])',
            '            pos += DVZIP_BLOCK_BYTES',
            '            if skip_left > 0:',
            '                skip_left -= 1',
            '                yield _dvzip_stored(block)',
            '            else:',
            '                out = _dvzip_block(block)',
            '                if int.from_bytes(out[:4], \'big\') & DVZIP_STORED:',
            '                    incomp_streak += 1',
            '                    if incomp_streak >= 4:',
            '                        skip_left = 32',
            '                else:',
            '                    incomp_streak = 0',
            '                yield out',
            '        if pos >= 4 * DVZIP_BLOCK_BYTES:',
            '            del held[:pos]',
            '            pos = 0',
            '    if pos < len(held):',
            '        tail = bytes(memoryview(held)[pos:])',
            '        yield _dvzip_stored(tail) if skip_left > 0 else _dvzip_block(tail)',
        ]),
    ),
]
changed = False
for old, new in TRANSFER_PATCHES:
    if new in text:
        continue
    if text.count(old) != 1:
        sys.exit(f'transfer.py: expected line moved ({old[:48]!r}); update patch-engine.py')
    text = text.replace(old, new, 1)
    changed = True
if changed:
    transfer_path.write_text(text, encoding='utf-8')
    print('transfer: 64 KiB DVZip + adaptive stored bypass + 0.5 s tail wait')

# sender.py & receiver.py: 2 MiB socket buffers and TCP_NODELAY so bursty AWDL
# presence slots do not stall behind Winsock's 64 KiB default TCP window.
sender_path = pathlib.Path(sys.argv[1]) / 'sender.py'
text = sender_path.read_text(encoding='utf-8')
old_s = NL.join([
    '        try:',
    '            sock.settimeout(self.timeout)',
    '            sock.connect((self.host, self.port, 0, self.scope))',
])
new_s = NL.join([
    '        try:',
    '            sock.settimeout(self.timeout)',
    '            for opt in ((socket.IPPROTO_TCP, socket.TCP_NODELAY, 1),',
    '                        (socket.SOL_SOCKET, socket.SO_SNDBUF, 2 * 1024 * 1024),',
    '                        (socket.SOL_SOCKET, socket.SO_RCVBUF, 2 * 1024 * 1024)):',
    '                try:',
    '                    sock.setsockopt(*opt)',
    '                except OSError:',
    '                    pass',
    '            sock.connect((self.host, self.port, 0, self.scope))',
])
if new_s not in text:
    if text.count(old_s) != 1:
        sys.exit('sender.py: Connection.connect moved; update patch-engine.py')
    text = text.replace(old_s, new_s, 1)
    sender_path.write_text(text, encoding='utf-8')
    print('sender: 2 MiB socket buffers + TCP_NODELAY')

receiver_path = pathlib.Path(sys.argv[1]) / 'receiver.py'
text = receiver_path.read_text(encoding='utf-8')
legacy_media = NL.join([
    '# Version 1 with no codecs listed makes the sender convert to legacy formats',
    '# (JPEG, not HEIF). The key has to be present in both answers.',
    "MEDIA_CAPABILITIES = json.dumps({'Version': 1}).encode()",
])
original_media = NL.join([
    '# AetherDrop stores media bytes directly; it does not require a legacy export.',
    '# Use the macOS capability schema documented by OpenDrop, including HEVC',
    '# Main/Main10 level 6.2 so high-frame-rate/4K clips need not become H.264/30fps.',
    '# This describes file acceptance, not the installed Windows playback codecs.',
    'MEDIA_CAPABILITIES = json.dumps({',
    "    'Version': 1,",
    "    'Codecs': {'hvc1': {'Profiles': {",
    "        'VTPerProfileSupport': {",
    "            '1': {'VTMaxPlaybackLevel': 186},",
    "            '2': {'VTMaxPlaybackLevel': 186},",
    "            '3': {},",
    '        },',
    "        'VTSupportedProfiles': [1, 2, 3],",
    '    }}},',
    "    'ContainerFormats': {'public.heif-standard': {",
    "        'HeifSubtypes': ['public.avci', 'public.heic', 'public.heif'],",
    '    }},',
    '}).encode()',
])
if original_media not in text:
    if text.count(legacy_media) != 1:
        sys.exit('receiver.py: media capability declaration moved; update original-media patch')
    text = text.replace(legacy_media, original_media, 1)
    receiver_path.write_text(text, encoding='utf-8')
connection_hook = NL.join([
    "        logging.info('connection from [%s]:%s', client_address[0], client_address[1])",
    "        opened = getattr(self.watcher, 'connection_opened', None)",
    '        if opened is not None:',
    '            opened(client_address[0])',
])
if connection_hook not in text:
    target = "        logging.info('connection from [%s]:%s', client_address[0], client_address[1])"
    if text.count(target) != 1:
        sys.exit('receiver.py: connection logging moved; update negotiation patch')
    text = text.replace(target, connection_hook, 1)
    receiver_path.write_text(text, encoding='utf-8')
old_r = NL.join([
    '    def setup(self):',
    '        self.request.settimeout(transfer.READ_IDLE_TIMEOUT_SECONDS)',
])
new_r = NL.join([
    '    def setup(self):',
    '        self.request.settimeout(transfer.READ_IDLE_TIMEOUT_SECONDS)',
    '        for opt in ((socket.IPPROTO_TCP, socket.TCP_NODELAY, 1),',
    '                    (socket.SOL_SOCKET, socket.SO_RCVBUF, 2 * 1024 * 1024),',
    '                    (socket.SOL_SOCKET, socket.SO_SNDBUF, 2 * 1024 * 1024)):',
    '            try:',
    '                self.request.setsockopt(*opt)',
    '            except OSError:',
    '                pass',
])
if new_r not in text:
    if text.count(old_r) != 1:
        sys.exit('receiver.py: Handler.setup moved; update patch-engine.py')
    text = text.replace(old_r, new_r, 1)
    receiver_path.write_text(text, encoding='utf-8')
    print('receiver: 2 MiB socket buffers + TCP_NODELAY')


# Contacts Only: AetherDrop_identity.py (copied beside the engine by
# build-installer.ps1) gives the receiver and the sender an Apple ID identity
# when the client set AETHERDROP_IDENTITY: the Apple-signed certificate on TLS,
# the validation record in /Discover and /Ask, and a Contacts Only filter on
# the receiver (AETHERDROP_RECEIVE=contacts). Without one nothing changes.
# The hooks below import it, so the patch brings it along: engine-test ran the
# patched engine without it and every test module of receiver/sender/engine
# failed to import (ModuleNotFoundError) from 2.1.0 on.
_identity = pathlib.Path(__file__).resolve().parent.parent / 'installer' / 'AetherDrop_identity.py'
(pathlib.Path(sys.argv[1]) / 'AetherDrop_identity.py').write_bytes(_identity.read_bytes())


def _hook(name, pairs, mark):
    path = pathlib.Path(sys.argv[1]) / name
    text = path.read_text(encoding='utf-8')
    if mark in text:
        return
    for old, new in pairs:
        if text.count(old) != 1:
            sys.exit(f'{name}: {old[:60]!r} moved; update patch-engine.py')
        text = text.replace(old, new, 1)
    path.write_text(text, encoding='utf-8')
    print(f'{name}: Apple ID identity (Contacts Only)')


_hook('receiver.py', [
    (NL + 'import mdns' + NL, NL + 'import mdns' + NL + 'import AetherDrop_identity' + NL),
    (NL.join([
        "        if self.path == '/Discover':",
        '            self.answer(self.identity_plist())',
        '        else:',
        '            self.handle_ask(body)',
    ]), NL.join([
        '        try:',
        "            sender_record = plistlib.loads(body).get('SenderRecordData') if body else None",
        '        except Exception:',
        '            sender_record = None',
        '        if not AetherDrop_identity.allowed(sender_record):',
        "            self.reject(401, 'not one of the contacts (Contacts Only)')",
        '            return',
        "        if self.path == '/Discover':",
        '            self.answer(self.identity_plist())',
        '        else:',
        '            self.handle_ask(body)',
    ])),
    ("            'ReceiverModelName': settings.model," + NL,
     "            'ReceiverModelName': settings.model," + NL + '            **AetherDrop_identity.receiver_extra(),' + NL),
    (NL.join([
        '    context.load_cert_chain(certificate, key)',
        '    # Senders present self-signed certificates, so none is asked for.',
    ]), NL.join([
        '    if not AetherDrop_identity.load_into(context):',
        '        context.load_cert_chain(certificate, key)',
        '    # Senders present self-signed certificates, so none is asked for.',
    ])),
], 'import AetherDrop_identity')

_hook('sender.py', [
    (NL + 'import mdns' + NL, NL + 'import mdns' + NL + 'import AetherDrop_identity' + NL),
    (NL.join([
        '    context.verify_mode = ssl.CERT_NONE',
        '    return context',
    ]), NL.join([
        '    context.verify_mode = ssl.CERT_NONE',
        '    # With an Apple ID identity the Apple-signed certificate goes on: an',
        '    # iPhone takes that one (a self-signed one it refuses, see above).',
        '    AetherDrop_identity.load_into(context)',
        '    return context',
    ])),
    ("post_plist(connection, '/Discover', {})", "post_plist(connection, '/Discover', AetherDrop_identity.sender_body({}))"),
    ("        'SenderModelName': DEFAULT_MODEL," + NL,
     "        'SenderModelName': DEFAULT_MODEL," + NL + '        **AetherDrop_identity.sender_extra(),' + NL),
], 'import AetherDrop_identity')
