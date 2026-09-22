import json
import tempfile
import unittest
from pathlib import Path
from deletion_trial_console import Trial, TrialError, TITLE


class TrialTests(unittest.TestCase):
    def test_create_is_guarded_and_filters_tokens(self):
        with tempfile.TemporaryDirectory() as directory:
            guard = Path(directory) / 'guard.json'
            calls = []
            def transport(api, body):
                self.assertTrue(guard.exists())
                self.assertEqual(json.loads(guard.read_text())['customId'], body['CustomId'])
                calls.append(api)
                return {'NewlyCreated': True, 'PlayFabId': 'ABC123', 'SessionTicket': 'never-export',
                        'EntityToken': {'EntityToken': 'never-export'}}
            trial = Trial('synthetic-secret', transport, guard)
            self.assertEqual(trial.execute('create_disposable'),
                {'newlyCreated': True, 'playerId': 'ABC123', 'titleId': TITLE})
            self.assertNotIn('synthetic-secret', guard.read_text())
            with self.assertRaises(TrialError): trial.execute('create_disposable')
            self.assertEqual(len(calls), 1)

    def test_unknown_survives_restart_and_never_recreates(self):
        with tempfile.TemporaryDirectory() as directory:
            guard = Path(directory) / 'guard.json'
            def lost(api, body): raise RuntimeError('sensitive-upstream')
            with self.assertRaises(RuntimeError): Trial('secret', lost, guard).execute('create_disposable')
            calls = []
            def inspect(api, body):
                calls.append(body)
                return {'NewlyCreated': False, 'PlayFabId': 'ABC123'}
            restarted = Trial('secret', inspect, guard)
            with self.assertRaises(FileExistsError): restarted.execute('create_disposable')
            self.assertEqual(calls, [])
            self.assertFalse(restarted.execute('inspect_attempt')['deletionEligible'])
            self.assertIs(calls[0]['CreateAccount'], False)

    def test_existing_or_malformed_account_rejected(self):
        for result in ({'NewlyCreated': False, 'PlayFabId':'ABC'},
                       {'NewlyCreated': True, 'PlayFabId':''}, {'NewlyCreated':'true','PlayFabId':'ABC'}):
            with self.subTest(result=result), tempfile.TemporaryDirectory() as directory:
                trial = Trial('secret', lambda api, body:result, Path(directory)/'guard')
                with self.assertRaises(TrialError): trial.execute('create_disposable')

    def test_delete_and_arbitrary_api_not_allowed(self):
        calls=[]
        trial=Trial('secret',lambda api,body:calls.append(api))
        for operation in ('delete','Server/DeletePlayer','publish',''):
            with self.assertRaises(TrialError): trial.execute(operation)
        self.assertEqual(calls,[])


if __name__ == '__main__': unittest.main()
