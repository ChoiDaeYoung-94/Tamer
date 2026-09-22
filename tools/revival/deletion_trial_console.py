"""Local masked-input broker for the approved disposable-title trial. Never deletes a player.

No command-line/environment secret input. The key lives only in this process.
The fixed private directory carries non-secret commands and sanitized results.
"""
import json
import secrets
import threading
import urllib.request
from pathlib import Path

TITLE = '12B656'
ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / 'Logs' / 'revival' / 'deletion-trial'


class TrialError(Exception):
    pass


class Trial:
    def __init__(self, secret, transport=None, guard=None):
        self._secret = secret
        self._transport = transport or self._request
        self.created = False
        self.custom = None
        self.player = None
        self.guard = guard or PRIVATE / 'creation-attempted.json'

    def _request(self, api, body):
        # Fixed origin, no redirects, no retries, no exception payloads returned.
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, *args, **kwargs):
                return None
        request = urllib.request.Request('https://' + TITLE + '.playfabapi.com/' + api,
            data=json.dumps(body).encode('utf-8'),
            headers={'X-SecretKey': self._secret, 'Content-Type': 'application/json'})
        try:
            with urllib.request.build_opener(NoRedirect).open(request, timeout=25) as response:
                data = json.loads(response.read(4 * 1024 * 1024))
            if data.get('code') != 200 or not isinstance(data.get('data'), dict):
                raise TrialError()
            return data['data']
        except Exception:
            raise TrialError('request_failed_or_response_unknown') from None

    def execute(self, operation):
        if operation == 'read_versions':
            return self._transport('Admin/GetCloudScriptVersions', {})
        if operation == 'create_disposable':
            if self.created or self.custom is not None:
                raise TrialError('creation_already_attempted')
            self.custom = 'deletion-disposable-' + secrets.token_hex(24)
            # This private credential is recoverable; title secret and session tokens are never stored.
            with self.guard.open('x', encoding='utf-8') as marker:
                marker.write(json.dumps({'titleId': TITLE, 'customId': self.custom, 'attempted': True}))
                marker.flush()
                import os
                os.fsync(marker.fileno())
            result = self._transport('Server/LoginWithCustomID',
                {'CustomId': self.custom, 'CreateAccount': True})
            import re
            if result.get('NewlyCreated') is not True or not re.fullmatch(r'[A-Fa-f0-9]{1,32}', result.get('PlayFabId', '')):
                raise TrialError('new_account_not_proven')
            self.player = result['PlayFabId']
            self.created = True
            # Session/entity tokens never leave the broker.
            return {'newlyCreated': True, 'playerId': self.player, 'titleId': TITLE}
        if operation == 'inspect_attempt':
            record = json.loads(self.guard.read_text(encoding='utf-8'))
            if record.get('titleId') != TITLE or not isinstance(record.get('customId'), str):
                raise TrialError('invalid_private_guard')
            result = self._transport('Server/LoginWithCustomID',
                {'CustomId': record['customId'], 'CreateAccount': False})
            return {'titleId': TITLE, 'playerId': result.get('PlayFabId'),
                'newlyCreatedProven': False, 'deletionEligible': False}
        raise TrialError('operation_not_allowed')


def main():
    import tkinter as tk
    PRIVATE.mkdir(parents=True, exist_ok=True)
    ready = PRIVATE / 'status.json'
    inbox = PRIVATE / 'command.json'
    result_path = PRIVATE / 'result.json'
    session_nonce = secrets.token_hex(24)
    # Never carry an old command into a newly unlocked secret session.
    if inbox.exists():
        raise TrialError('stale_command_requires_review')
    trial = None
    busy = False
    window = tk.Tk()
    window.title('Tamer deletion trial - local secret input')
    window.geometry('640x260')
    tk.Label(window, text='시험 타이틀 12B656 전용 비밀키 입력', font=('Malgun Gothic', 14)).pack(pady=14)
    tk.Label(window, text='키는 화면에서 가려지며 메모리에만 보관됩니다.\n채팅에 보내지 마세요. 입력만으로 계정 생성/삭제/설정 변경을 실행하지 않습니다.').pack()
    entry = tk.Entry(window, show='●', width=65)
    entry.pack(pady=12)
    status = tk.StringVar(value='사용자 입력 대기')
    tk.Label(window, textvariable=status).pack()

    def write(path, value):
        temporary = path.with_suffix('.tmp')
        temporary.write_text(json.dumps(value, ensure_ascii=False), encoding='utf-8')
        temporary.replace(path)

    def unlock():
        nonlocal trial
        value = entry.get()
        if not value or len(value) > 4096 or not value.isascii() or any(c.isspace() for c in value):
            status.set('입력 형식을 확인해 주세요. 값은 표시하지 않습니다.')
            return
        trial = Trial(value)
        entry.delete(0, tk.END)
        entry.configure(state='disabled')
        unlock_button.configure(state='disabled')
        status.set('입력 완료 — 준비됨. 삭제 기능은 이 도구에 없습니다.')
        write(ready, {'ready': True, 'titleId': TITLE, 'nonce': session_nonce})

    unlock_button = tk.Button(window, text='메모리에만 보관', command=unlock)
    unlock_button.pack(pady=10)
    write(ready, {'ready': False, 'titleId': TITLE, 'nonce': session_nonce})

    def poll():
        nonlocal busy
        if trial is not None and not busy and inbox.exists():
            busy = True
            try:
                command = json.loads(inbox.read_text(encoding='utf-8-sig'))
                inbox.unlink()
                operation = command['operation']
                command_id = command['id']
                if command.get('nonce') != session_nonce or operation not in ('read_versions', 'create_disposable', 'inspect_attempt') or not isinstance(command_id, str):
                    raise TrialError()
            except Exception:
                write(result_path, {'ok': False, 'code': 'command_rejected'})
                busy = False
            else:
                def work():
                    nonlocal busy
                    try:
                        result = trial.execute(operation)
                        write(result_path, {'ok': True, 'id': command_id, 'result': result})
                    except Exception:
                        write(result_path, {'ok': False, 'id': command_id, 'code': 'failed_or_unknown_no_retry'})
                    finally:
                        busy = False
                threading.Thread(target=work, daemon=True).start()
        window.after(300, poll)

    def close():
        nonlocal trial
        trial = None
        write(ready, {'ready': False, 'closed': True})
        window.destroy()
    window.protocol('WM_DELETE_WINDOW', close)
    window.after(300, poll)
    window.mainloop()


if __name__ == '__main__':
    main()
