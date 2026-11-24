import pathlib, sys, types, os
from unittest.mock import patch, Mock
root = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, os.environ.get('AETHERDROP_TEST_ENGINE', str(root / 'build/stage/airdrop')))
import engine

assert engine.UPLOAD_DUE_SECONDS == 180
events = Mock()
settings = types.SimpleNamespace(ask=False)
incoming = engine.Incoming(events, settings)
incoming.connection_opened('::1')
events.say.assert_called_with('negotiating', peer='::1')
events.reset_mock()
print('PASS incoming connection protects negotiation before the receive invitation exists')
ask = {'SenderComputerName': 'test phone', 'Files': [
    {'FileName': 'large-video.mov', 'FileSize': 300_000_000, 'FileType': 'public.movie'}]}
with patch.object(engine.threading, 'Timer') as timer:
    token = incoming.asked(ask, '::1', lambda: False)
    assert token.total == 300_000_000
    assert token.uploading is False
    assert timer.call_args.args[0] == 180
    assert timer.return_value.start.called
print('PASS 300 MB request is accepted and receives a 180-second preparation deadline')
incoming._overdue(token)
assert token.uploading is True
events.say.assert_called_with('lost', id=token.key, reason='nothing arrived')
print('PASS preparation still has a finite failure deadline')
events.reset_mock()
token.uploading = True
incoming._overdue(token)
events.say.assert_not_called()
print('PASS preparation deadline does not abort an upload already in progress')
