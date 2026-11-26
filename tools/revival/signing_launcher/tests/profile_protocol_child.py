"""Owned synthetic JKS/PKCS12 worker tests; no operating key or Unity access."""
from pathlib import Path
import hashlib,io,json,os,secrets,struct,subprocess,sys,types

SOURCE=Path(__file__).resolve().parent.parent
bridge=types.ModuleType('synthetic_profile_bridge');bridge.__file__=str(SOURCE/'producer_bridge.py')
exec(compile(Path(bridge.__file__).read_bytes(),bridge.__file__,'exec'),bridge.__dict__)
bridge.load_helpers({'pins':[{'path':str(bridge.HELPERS/name),'sha256':bridge.sha((bridge.HELPERS/name).read_bytes())}for name in bridge.SOURCES]})

def main():
    java,javac,keytool,classes,folder=sys.argv[1:6]
    root=Path(folder);rows=[];phase='owned_job';password=None
    try:
        bridge.require_owned_job()
        if not root.is_dir() or any(root.iterdir()):raise ValueError('FreshSyntheticDirectory')
        password=secrets.token_urlsafe(24)
        env={k:v for k,v in os.environ.items()if not k.upper().startswith(('TAMER_','JAVA_','JDK_','_JAVA_','CLASSPATH'))}
        env['PROFILE_SYNTHETIC_PASS']=password
        phase='compile_worker'
        made=subprocess.run([javac,'-encoding','UTF-8','-d',classes,str(SOURCE/'OperatingKeyPreflight.java')],
            cwd=root,env=env,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,
            timeout=30,creationflags=subprocess.CREATE_NO_WINDOW,close_fds=True)
        if made.returncode:raise ValueError('SyntheticCompilation')
        stores={}
        for kind in ('JKS','PKCS12'):
            phase='generate_'+kind;file=root/('synthetic.'+('jks'if kind=='JKS'else'p12'));cert=file.with_suffix('.der')
            args=[keytool,'-genkeypair','-storetype',kind,'-keystore',str(file),'-alias','synthetic-upload',
                '-keyalg','RSA','-keysize','2048','-validity','1','-dname','CN=Disposable Signing Profile Test',
                '-storepass:env','PROFILE_SYNTHETIC_PASS','-keypass:env','PROFILE_SYNTHETIC_PASS','-noprompt']
            result=subprocess.run(args,cwd=root,env=env,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,timeout=25,creationflags=subprocess.CREATE_NO_WINDOW,close_fds=True)
            if result.returncode:raise ValueError('SyntheticGeneration')
            result=subprocess.run([keytool,'-exportcert','-storetype',kind,'-keystore',str(file),'-alias','synthetic-upload',
                '-storepass:env','PROFILE_SYNTHETIC_PASS','-file',str(cert)],cwd=root,env=env,stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=15,creationflags=subprocess.CREATE_NO_WINDOW,close_fds=True)
            if result.returncode:raise ValueError('SyntheticCertificate')
            stores[kind]=(file.read_bytes(),cert.read_bytes())
        def frame(kind,data=None,der=None,alias=b'synthetic-upload',store=None,key=None,magic=None):
            source,cert=stores[kind];out=bytearray(struct.pack('>I',magic or (0x544b5031 if kind=='JKS'else 0x544b5032)))
            for value in (source if data is None else data,cert if der is None else der,alias):
                out.extend(struct.pack('>I',len(value)));out.extend(value)
            for value in (password if store is None else store,password if key is None else key):
                raw=value.encode('utf-16-be');out.extend(struct.pack('>I',len(raw)//2));out.extend(raw)
            return out
        def check(name,raw,expected):
            nonlocal phase
            phase=name
            try:
                result=subprocess.run([java,'-XX:-UsePerfData','-XX:+DisableAttachMechanism','-XX:ErrorFile=NUL',
                    '-XX:-CreateCoredumpOnCrash','-cp',classes,'OperatingKeyPreflight'],input=raw,
                    cwd=root,env={k:v for k,v in env.items()if k!='PROFILE_SYNTHETIC_PASS'},stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,timeout=15,creationflags=subprocess.CREATE_NO_WINDOW,close_fds=True)
                if result.stderr or len(result.stdout)>4096:raise ValueError('WorkerOutput')
                value=json.loads(result.stdout)
                if set(value)!={'status','exceptionType'}:raise ValueError('WorkerContract')
                success=value=={'status':'verified','exceptionType':''}and result.returncode==0
                if success!=expected or(not expected and(result.returncode!=1 or value['status']!='failed')):
                    raise ValueError('UnexpectedWorkerResult')
                rows.append({'case':name,'passed':True,'workerVerified':success,'exceptionType':value['exceptionType']})
            finally:raw[:]=b'\0'*len(raw)
        check('legacy_jks_valid',frame('JKS'),True)
        check('external_pkcs12_valid',frame('PKCS12'),True)
        check('pkcs12_wrong_store_password',frame('PKCS12',store='unit-only-wrong',key='unit-only-wrong'),False)
        check('pkcs12_different_key_password',frame('PKCS12',key='unit-only-other'),False)
        check('pkcs12_wrong_certificate',frame('PKCS12',der=stores['JKS'][1]),False)
        check('pkcs12_wrong_alias',frame('PKCS12',alias=b'other-upload'),False)
        check('pkcs12_uppercase_alias',frame('PKCS12',alias=b'Synthetic-upload'),False)
        check('pkcs12_malformed_utf8_alias',frame('PKCS12',alias=b'\xff'),False)
        check('pkcs12_rejects_jks_fallback',frame('PKCS12',data=stores['JKS'][0]),False)
        check('jks_rejects_pkcs12_fallback',frame('JKS',data=stores['PKCS12'][0]),False)
        check('pkcs12_trailing_der',frame('PKCS12',der=stores['PKCS12'][1]+b'x'),False)
        check('unknown_protocol',frame('PKCS12',magic=0x544b5033),False)
        check('partial_frame',frame('PKCS12')[:-1],False)
        check('trailing_frame',frame('PKCS12')+b'x',False)
        check('oversized_store_field',bytearray(struct.pack('>II',0x544b5032,16*1024*1024+1)),False)
        # Exercise the real bridge challenge and its owned worker, not only main().
        private=root/'external';bridge.producer.create_private_directory(private)
        keyfile=private/'synthetic.p12';keyfile.write_bytes(stores['PKCS12'][0])
        certfile=private/'synthetic.der';certfile.write_bytes(stores['PKCS12'][1])
        checkout=root/'checkout';checkout.mkdir()
        profile={'schema':1,'profile':'external_pkcs12_upload','storeType':'PKCS12','keystore':str(keyfile),
            'allowedRoot':str(private),'keystoreSha256':bridge.sha(keyfile.read_bytes()),'alias':'synthetic-upload',
            'certificateSha256':bridge.sha(certfile.read_bytes()),'playAppSigningCertificateSha256':'9'*64,
            'activationEvidence':str(private/'never-consumed-activation.json'),'activationEvidenceSha256':'8'*64}
        plan={'schema':3,'checkout':str(checkout),'signingProfile':profile}
        value={'schema':2,'checkout':str(checkout),'alias':'synthetic-upload','certificate':str(certfile),
            'java':java,'classDirectory':classes}
        phase='real_bridge_pkcs12_challenge';bridge.challenge(value,[password,password],plan)
        rows.append({'case':phase,'passed':True,'workerVerified':True})
        return {'passed':True,'cases':rows,'actualChallengeVerified':True,'workerCompilations':1,
            'syntheticKeyStoresGenerated':2,'actualOperatingKeyUses':0,'actualUnityBuildCalls':0,
            'parentAndWorkerJobRequired':True,'sourceSha256':bridge.sha((SOURCE/'OperatingKeyPreflight.java').read_bytes())}
    except BaseException as failure:
        return {'passed':False,'phase':phase,'exceptionType':type(failure).__name__,'cases':rows,
            'actualOperatingKeyUses':0,'actualUnityBuildCalls':0}
    finally:
        if password is not None:password=None
        if 'env'in locals():env.clear()

if __name__=='__main__':
    result=main();print(json.dumps(result),end='');raise SystemExit(0 if result['passed']else 1)
