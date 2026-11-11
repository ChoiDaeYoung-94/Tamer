from copy import deepcopy
from datetime import datetime, timedelta, timezone
import unittest
import signing_profile as p


class SigningProfileTests(unittest.TestCase):
    def setUp(self):
        self.profile={'schema':1,'profile':p.EXTERNAL,'storeType':'PKCS12',
            'keystore':'D:/private/key-set/upload.p12','allowedRoot':'D:/private/key-set',
            'keystoreSha256':'a'*64,'alias':'upload-20261111','certificateSha256':'b'*64,
            'playAppSigningCertificateSha256':'d'*64,
            'activationEvidence':'C:/private/activation.json','activationEvidenceSha256':'c'*64}
        self.plan={'schema':3,'checkout':'D:/work/Tamer','signingProfile':self.profile}
        self.now=datetime(2026,11,14,1,tzinfo=timezone.utc)
        self.evidence={'schema':1,'applicationId':'com.AeDeong.MonsterTamer','status':'active',
            'certificateSha256':'b'*64,'playAppSigningCertificateSha256':'d'*64,
            'observedUtc':self.now.isoformat(),'validFromUtc':(self.now-timedelta(days=1)).isoformat(),
            'source':'user_current_console'}

    def test_legacy_selection_preserves_original_jks_and_protocol(self):
        s=p.select({'schema':2,'checkout':'D:/work/Tamer','before':{'src/AeDeong.keystore':{'sha256':'a'*64}}})
        self.assertEqual((s.store_type,s.worker_magic,s.keystore),('JKS',0x544B5031,'D:/work/Tamer/src/AeDeong.keystore'))
        self.assertFalse(s.external)

    def test_external_selection_is_explicit_and_keeps_key_outside_checkout(self):
        s=p.select(self.plan)
        self.assertEqual((s.store_type,s.worker_magic,s.alias),('PKCS12',0x544B5032,'upload-20261111'))
        self.assertTrue(s.external)
        self.assertTrue(p.require_activation(self.evidence,s,expected_play_signer='d'*64,now=self.now))

    def test_native_windows_separators_keep_existing_plan_compatibility(self):
        q=deepcopy(self.plan);q['checkout']='D:\\work\\Tamer'
        q['signingProfile'].update(keystore='D:\\private\\key-set\\upload.p12',allowedRoot='D:\\private\\key-set')
        self.assertEqual(p.select(q).keystore,'D:/private/key-set/upload.p12')

    def test_legacy_posix_plan_remains_available_for_existing_pure_tests(self):
        s=p.select({'schema':2,'checkout':'/tmp/Tamer','before':{'src/AeDeong.keystore':{'sha256':'a'*64}}})
        self.assertEqual(s.keystore,'/tmp/Tamer/src/AeDeong.keystore')

    def test_unknown_schema_profile_or_store_format_never_falls_back(self):
        for field,value in [('schema',True),('schema',4),('signingProfile',None)]:
            with self.subTest(field=field,value=value):
                q=deepcopy(self.plan);q[field]=value
                with self.assertRaises(ValueError):p.select(q)
        for field,value in [('schema',True),('profile','legacy_jks'),('storeType','JKS'),('extra',True)]:
            with self.subTest(field=field,value=value):
                q=deepcopy(self.plan);q['signingProfile'][field]=value
                with self.assertRaises(ValueError):p.select(q)

    def test_legacy_schema_refuses_smuggled_external_profile(self):
        q=deepcopy(self.plan);q['schema']=2
        with self.assertRaises(ValueError):p.select(q)

    def test_wrong_private_root_checkout_key_and_broad_root_are_rejected(self):
        for key,root in [('D:/work/Tamer/src/key.p12','D:/work/Tamer/src'),
                         ('D:/private/other/key.p12','D:/private/key-set'),
                         ('D:/key.p12','D:/'),('D:/private/key-set/key.jks','D:/private/key-set')]:
            with self.subTest(key=key,root=root):
                q=deepcopy(self.plan);q['signingProfile'].update(keystore=key,allowedRoot=root)
                with self.assertRaises(ValueError):p.select(q)

    def test_ambiguous_windows_paths_are_rejected(self):
        for path in ['D:/private/../key.p12','D:relative.p12','//server/share/key.p12',
                     'D:/private//key.p12','D:/private/NUL.p12','D:/private/key.p12:stream',
                     'D:/private/key.p12.','D:/private/key.p12 ','D:/private/key?.p12',
                     'D:/private/key\np12']:
            with self.subTest(path=path):
                with self.assertRaises(ValueError):p.local_path(path)

    def test_malformed_alias_and_hash_are_rejected(self):
        for field,value in [('alias',''),('alias','Upload'),('alias','a\0b'),('keystoreSha256','A'*64),('certificateSha256','b'*63)]:
            with self.subTest(field=field):
                q=deepcopy(self.plan);q['signingProfile'][field]=value
                with self.assertRaises(ValueError):p.select(q)

    def test_pending_reset_and_unrecognized_evidence_cannot_authorize_signing(self):
        for field,value in [('status','processing'),('status','scheduled'),('source','draft'),('schema',True),('extra',True)]:
            with self.subTest(field=field,value=value):
                q=deepcopy(self.evidence);q[field]=value
                with self.assertRaises(ValueError):p.require_activation(q,p.select(self.plan),expected_play_signer='d'*64,now=self.now)

    def test_changed_upload_or_play_certificate_is_rejected(self):
        for field in ['certificateSha256','playAppSigningCertificateSha256']:
            q=deepcopy(self.evidence);q[field]='e'*64
            with self.assertRaises(ValueError):p.require_activation(q,p.select(self.plan),expected_play_signer='d'*64,now=self.now)

    def test_profile_cannot_substitute_upload_key_for_play_signer(self):
        q=deepcopy(self.plan);q['signingProfile']['playAppSigningCertificateSha256']='b'*64
        with self.assertRaises(ValueError):p.select(q)
        with self.assertRaises(ValueError):p.require_activation(self.evidence,p.select(self.plan),expected_play_signer='e'*64,now=self.now)

    def test_future_stale_unzoned_or_unobserved_activation_is_rejected(self):
        for field,value in [('observedUtc',(self.now+timedelta(seconds=1)).isoformat()),
                            ('observedUtc',(self.now-timedelta(days=2)).isoformat()),
                            ('validFromUtc',(self.now+timedelta(seconds=1)).isoformat()),
                            ('validFromUtc','2026-11-13T17:23:00'),
                            ('validFromUtc','2026-11-13T17:23:00+09:00')]:
            with self.subTest(field=field,value=value):
                q=deepcopy(self.evidence);q[field]=value
                with self.assertRaises(ValueError):p.require_activation(q,p.select(self.plan),expected_play_signer='d'*64,now=self.now)


if __name__=='__main__':unittest.main()
