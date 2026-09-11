"""Read-only official API adapters; credentials supplied by the server host."""
import json
import re
import urllib.request
from urllib.parse import quote

from .verification import Rejected


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None  # Never forward a ticket or secret to a redirect target.


def request_json(url, headers, body=None):
    request = urllib.request.Request(url, headers=headers,
        data=None if body is None else json.dumps(body).encode())
    with urllib.request.build_opener(NoRedirect()).open(request, timeout=15) as response:
        raw = response.read(262145)
        if len(raw) > 262144:
            raise Rejected('upstream_unavailable')
        return json.loads(raw)


class Upstream:
    def __init__(self, title_id, secret_key, access_token, request=request_json):
        if not re.fullmatch(r'[A-Za-z0-9]+', title_id) or not secret_key:
            raise ValueError('Server credential configuration required')
        self.title_id, self.secret_key = title_id, secret_key
        self.access_token, self.request = access_token, request

    def authenticate(self, ticket):
        result = self.request('https://' + self.title_id + '.playfabapi.com/Server/AuthenticateSessionTicket',
            {'X-SecretKey': self.secret_key, 'Content-Type': 'application/json'},
            {'SessionTicket': ticket})
        data = result.get('data', {})
        account = data.get('UserInfo', {}).get('PlayFabId')
        if (result.get('code') != 200 or data.get('IsSessionTicketExpired') is not False
                or not isinstance(account, str) or not account):
            raise Rejected('authentication_failed')
        return account

    def get_purchase(self, package, token):
        return self.request('https://androidpublisher.googleapis.com/androidpublisher/v3/applications/'
            + quote(package, safe='') + '/purchases/productsv2/tokens/' + quote(token, safe=''),
            {'Authorization': 'Bearer ' + self.access_token()})
