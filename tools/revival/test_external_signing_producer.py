"""Synthetic metadata and files only; never invokes Java, Unity or real keys."""
from datetime import datetime,timedelta,timezone
import hashlib,json,os,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import test_private_ads_producer as legacy
import private_ads_producer as producer
import signing_profile as profiles


class ExternalProducerTests(unittest.TestCase):
    def setUp(self):
        legacy.ProducerTests.setUp(self)
        other=tempfile.TemporaryDirectory();self.addCleanup(other.cleanup)
        self.private=Path(other.name)/'private';producer.create_private_directory(self.private)
        self.key=self.private/'upload.p12';self.key.write_bytes(b'SYNTHETIC ENCRYPTED STORE; NOT A REAL KEY')
        self.activation=self.private/'activation.json'
        now=datetime.now(timezone.utc)
        self.active={'schema':1,'applicationId':'com.AeDeong.MonsterTamer','status':'active',
            'certificateSha256':'b'*64,'playAppSigningCertificateSha256':'d'*64,
            'observedUtc':now.isoformat(),'validFromUtc':(now-timedelta(days=1)).isoformat(),
            'source':'user_current_console'}
        self.profile={'schema':1,'profile':profiles.EXTERNAL,'storeType':'PKCS12',
            'keystore':str(self.key),'allowedRoot':str(self.private),'keystoreSha256':self.sha(self.key),
            'alias':'synthetic-upload','certificateSha256':'b'*64,'playAppSigningCertificateSha256':'d'*64,
            'activationEvidence':str(self.activation),'activationEvidenceSha256':'c'*64}
        self.write_activation()

    @staticmethod
    def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

    def write_activation(self):
        self.activation.write_text(json.dumps(self.active),encoding='utf-8')
        self.profile['activationEvidenceSha256']=self.sha(self.activation)

    def prepare_external(self):
        result=producer.prepare_plan(self.root,self.config,self.head,'synthetic',branch='synthetic',
            preflight_result={'disabledPreparationValid':True,'configSha256':producer.base.digest(self.config.read_bytes())},
            signing_profile=self.profile)
        path=Path(result['plan']);plan=producer.load_plan(self.root,path,result['planSha256'])
        plan['_reviewedSha256']=result['planSha256'];return path,plan

    def signing(self,plan):
        selected=profiles.select(plan)
        return {'schema':2,'keystore':selected.keystore,'keystoreSha256':selected.keystore_sha256,
            'alias':selected.alias,'certificateSha256':selected.certificate_sha256,
            'existingReleaseKeyVerified':True,'sourceHead':plan['head'],'planSha256':plan['_reviewedSha256'],
            'signingProfile':profiles.EXTERNAL,'storeType':'PKCS12',
            'activationEvidenceSha256':self.profile['activationEvidenceSha256']}

    def write_signing(self,path,value):
        file=path.parent/'synthetic-signing.json';file.write_text(json.dumps(value),encoding='utf-8');return file

    def test_external_plan_keeps_mode_receipt_and_original_key_without_export(self):
        path,plan=self.prepare_external()
        self.assertEqual((plan['schema'],plan['receiptModeVersion']),(3,1))
        self.assertIn(producer.KEY,plan['before'])
        self.assertFalse(plan['producerPrepared'] or plan['binaryVerified'] or plan['distributable'])
        self.assertFalse(any(p.read_bytes()==self.key.read_bytes() for p in path.parent.glob('*.snapshot')))

    def test_processing_reset_is_rejected_before_external_key_identity_read(self):
        self.active['status']='processing';self.write_activation();_,plan=self.prepare_external()
        seen=[];original=producer.base.identity
        def identity(path):seen.append(Path(path));return original(path)
        with patch.object(producer.base,'identity',side_effect=identity),self.assertRaises(ValueError):
            producer.external_signing_bindings(plan)
        self.assertNotIn(self.key,seen)

    def test_changed_encrypted_key_is_rejected_without_a_receipt(self):
        path,plan=self.prepare_external();self.key.write_bytes(b'REPLACED SYNTHETIC STORE')
        with self.assertRaises(ValueError):producer.external_signing_bindings(plan)
        self.assertFalse((path.parent/'launcher-signing.private.json').exists())

    def test_receipt_requires_exact_schema_alias_certificate_format_and_same_password(self):
        path,plan=self.prepare_external();valid=self.signing(plan)
        with patch.dict(os.environ,{'TAMER_KEYSTORE_PASS':'unit-only-same','TAMER_KEYALIAS_PASS':'unit-only-same'}):
            self.assertEqual(producer.require_signing(self.root,plan,self.write_signing(path,valid)),valid)
            for field,value in [('schema',1),('schema',True),('alias','other'),('certificateSha256','e'*64),
                                ('storeType','JKS'),('signingProfile','legacy_jks'),('activationEvidenceSha256','e'*64)]:
                with self.subTest(field=field,value=value):
                    bad=dict(valid);bad[field]=value
                    with self.assertRaises(ValueError):producer.require_signing(self.root,plan,self.write_signing(path,bad))
            with patch.dict(os.environ,{'TAMER_KEYALIAS_PASS':'different-unit-only'}):
                with self.assertRaises(ValueError):producer.require_signing(self.root,plan,self.write_signing(path,valid))

    def test_legacy_receipt_cannot_redirect_to_an_external_path(self):
        path,_,plan=legacy.ProducerTests.prepare(self);plan['_reviewedSha256']=self.sha(path)
        value={'schema':1,'keystore':str(self.key),'keystoreSha256':self.sha(self.key),
            'alias':'synthetic-upload','certificateSha256':'b'*64,'existingReleaseKeyVerified':True,
            'sourceHead':plan['head'],'planSha256':plan['_reviewedSha256']}
        with patch.dict(os.environ,{'TAMER_KEYSTORE_PASS':'unit-only','TAMER_KEYALIAS_PASS':'unit-only'}):
            with self.assertRaises(ValueError):producer.require_signing(self.root,plan,self.write_signing(path,value))

    def test_future_activation_can_prepare_but_cannot_authorize_execution(self):
        self.active['validFromUtc']=(datetime.now(timezone.utc)+timedelta(days=2)).isoformat();self.write_activation()
        _,plan=self.prepare_external()
        with self.assertRaises(ValueError):producer.external_signing_bindings(plan)

    def test_external_bindings_cover_both_inputs_and_exact_hashes(self):
        _,plan=self.prepare_external();value=producer.external_signing_bindings(plan)
        self.assertEqual(set(value),{self.key,self.activation})
        self.assertEqual(value[self.key][1],self.profile['keystoreSha256'])
        self.assertEqual(value[self.activation][1],self.profile['activationEvidenceSha256'])

    def test_external_key_hardlink_is_rejected(self):
        _,plan=self.prepare_external();link=self.private/'same-key.p12';os.link(self.key,link)
        with self.assertRaises(ValueError):producer.external_signing_bindings(plan)

    def test_activation_evidence_hardlink_is_rejected(self):
        _,plan=self.prepare_external();link=self.private/'same-evidence.json';os.link(self.activation,link)
        with self.assertRaises(ValueError):producer.external_signing_bindings(plan)


if __name__=='__main__':unittest.main()
