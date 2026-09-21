"""Pinned-title PlayFab adapters. No environment reads, login, or I/O on import."""
from dataclasses import dataclass
import hashlib
import json
import re
import urllib.request

from .deletion import Rejected
from .title_deletion_provider import DeletionTarget


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def request_json(url, headers, body):
    # Default certificate validation, no ambient proxy or redirects carrying credentials.
    request = urllib.request.Request(url, data=json.dumps(body).encode(), headers=headers, method='POST')
    try:
        with urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect()).open(request, timeout=10) as response:
            raw = response.read(65537)
            if response.status != 200 or len(raw) > 65536 or response.headers.get_content_type() != 'application/json':
                raise ValueError()
            return json.loads(raw)
    except Exception:
        raise Rejected('unavailable') from None


def identifier(value):
    return isinstance(value, str) and 1 <= len(value) <= 128 and value.isascii() and value.replace('-', '').isalnum()


@dataclass(frozen=True, repr=False)
class VerifiedSession:
    title_id: str
    account: str
    entity_id: str
    account_type: str
    subject_binding: str = ''


class PlayFabSession:
    ROUTES = {'/Server/AuthenticateSessionTicket': 'SessionTicket',
              '/Server/GetUserAccountInfo': 'PlayFabId', '/Server/DeletePlayer': 'PlayFabId'}
    ANONYMOUS = {'AndroidDeviceInfo': ('AndroidDeviceId', 'android_device'),
                 'CustomIdInfo': ('CustomId', 'custom_id')}
    GOOGLE_PLAY_GAMES = ('GooglePlayGamesPlayerId', 'google_play_games')

    def __init__(self, title_id, secret_key, request=request_json, google_play_games_enabled=False):
        if not isinstance(title_id, str) or not re.fullmatch(r'[A-Za-z0-9]{3,32}', title_id):
            raise ValueError('Pinned title required')
        if not isinstance(secret_key, str) or not 1 <= len(secret_key) <= 4096 or any(c in secret_key for c in '\r\n'):
            raise ValueError('Server credential required')
        self.title_id, self._secret, self._request = title_id, secret_key, request
        self.google_play_games_enabled = google_play_games_enabled is True

    def invoke(self, title_id, route, body):
        field = self.ROUTES.get(route)
        if title_id != self.title_id or field is None or not isinstance(body, dict) or set(body) != {field}:
            raise Rejected('invalid_request')
        value = body[field]
        if field == 'SessionTicket':
            if not isinstance(value, str) or not 1 <= len(value) <= 4096:
                raise Rejected('session_confirmation_required')
        elif not identifier(value):
            raise Rejected('invalid_request')
        try:
            response = self._request('https://' + self.title_id + '.playfabapi.com' + route,
                {'Content-Type': 'application/json', 'X-SecretKey': self._secret}, body)
            if (not isinstance(response, dict) or type(response.get('code')) is not int
                    or response['code'] != 200 or response.get('status') != 'OK'
                    or response.get('error') or not isinstance(response.get('data'), dict)):
                raise ValueError()
            return response
        except Exception:
            raise Rejected('unavailable') from None

    @staticmethod
    def target_from_info(title, info):
        if not isinstance(info, dict):
            raise Rejected('session_confirmation_required')
        title_info = info.get('TitleInfo')
        entity = title_info.get('TitlePlayerAccount') if isinstance(title_info, dict) else None
        if (not identifier(info.get('PlayFabId')) or not isinstance(entity, dict)
                or entity.get('Type') != 'title_player_account' or not identifier(entity.get('Id'))):
            raise Rejected('session_confirmation_required')
        return DeletionTarget(title, info['PlayFabId'], entity['Id'])

    def session_account(self, info):
        # Origination is historical, not a list of current credentials. Ignore it.
        # Unknown/nonempty *Info and OpenIdInfo also make the choice ambiguous.
        linked = [key for key, value in info.items()
                  if key.endswith('Info') and key not in ('TitleInfo', 'PrivateInfo') and value not in (None, [])]
        private = info.get('PrivateInfo')
        supported = dict(self.ANONYMOUS)
        if self.google_play_games_enabled:
            supported['GooglePlayGamesInfo'] = self.GOOGLE_PLAY_GAMES
        if (len(linked) != 1 or linked[0] not in supported
                or info.get('Username') or (private is not None and
                    (not isinstance(private, dict) or private.get('Email')))):
            raise Rejected('account_type_unsupported')
        field, kind = supported[linked[0]]
        link = info[linked[0]]
        if not isinstance(link, dict) or not isinstance(link.get(field), str) or not link[field]:
            raise Rejected('account_type_unsupported')
        binding = ''
        if kind == 'google_play_games':
            subject = link[field]
            # An opaque provider ID, not a name/email or a client-supplied identity.
            # Do not infer that this ticket was issued through PGS from its linkage.
            if (not subject.isascii() or len(subject) > 1024
                    or any(c.isspace() or ord(c) < 32 or ord(c) == 127 for c in subject)):
                raise Rejected('account_type_unsupported')
            binding = hashlib.sha256(subject.encode('ascii')).hexdigest()
        return kind, binding

    def authenticate(self, ticket):
        data = self.invoke(self.title_id, '/Server/AuthenticateSessionTicket', {'SessionTicket': ticket})['data']
        if data.get('IsSessionTicketExpired') is not False:
            raise Rejected('session_confirmation_required')
        info = data.get('UserInfo')
        target = self.target_from_info(self.title_id, info)
        kind, binding = self.session_account(info)
        return VerifiedSession(self.title_id, target.account, target.title_entity_id, kind, binding)

    def resolve_target(self, account):
        data = self.invoke(self.title_id, '/Server/GetUserAccountInfo', {'PlayFabId': account})['data']
        info = data.get('UserInfo')
        target = self.target_from_info(self.title_id, info)
        self.session_account(info)
        if target.account != account:
            raise Rejected('operation_conflict')
        return target

    def verify_target(self, target):
        return isinstance(target, DeletionTarget) and target.title_id == self.title_id and self.resolve_target(target.account) == target
