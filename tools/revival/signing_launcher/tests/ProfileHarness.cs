using System;
using System.IO;
using System.Collections.Generic;
using System.Security;
using System.Text.Json;
using System.Threading;

public static class ProfileHarness {
    public static string Worker(string python,string script,string java,string javac,string keytool,string classes,string evidence) {
        return LauncherProcess.Run(LauncherProcess.Start(python,Path.GetDirectoryName(python),"-I","-S","-B",script,java,javac,keytool,classes,evidence),
            null,180000,CancellationToken.None,(output,error,code)=>{
                File.WriteAllBytes(Path.Combine(evidence,"actual-worker-response.private.json"),output);
                if(error.Length!=0)File.WriteAllBytes(Path.Combine(evidence,"actual-worker-error.private.bin"),error);
                using(var doc=LauncherProcess.Json(output,error,code)) {
                    if(!doc.RootElement.GetProperty("passed").GetBoolean())throw new InvalidOperationException("Synthetic worker result");
                    return doc.RootElement.GetRawText();
                }
            });
    }
    static LauncherBinding Binding() {
        return new LauncherBinding{Schema=2,Checkout=@"C:\synthetic\checkout",Alias="synthetic-upload",CertificateSha256=new string('b',64),
            SigningProfile=new LauncherSigningProfile{Schema=1,Profile="external_pkcs12_upload",StoreType="PKCS12",
                Keystore=@"D:\synthetic\private\upload.p12",AllowedRoot=@"D:\synthetic\private",KeystoreSha256=new string('a',64),
                Alias="synthetic-upload",CertificateSha256=new string('b',64),PlayAppSigningCertificateSha256=new string('c',64),
                ActivationEvidence=@"D:\synthetic\evidence\active.json",ActivationEvidenceSha256=new string('d',64)}};
    }
    public static string Run(string evidence) {
        var rows=new List<object>();
        Action<string,Action<LauncherBinding>,bool> check=(name,change,expected)=>{
            var b=Binding();change(b);bool accepted;
            try{accepted=SigningLauncher.ExternalSigning(b);}catch(SecurityException){accepted=false;}
            if(accepted!=expected)throw new InvalidOperationException("Profile case: "+name);
            rows.Add(new{Case=name,Passed=true,ExternalAccepted=accepted});
        };
        check("pkcs12_valid",b=>{},true);
        check("unknown_schema",b=>b.Schema=3,false);
        check("legacy_profile_injection",b=>b.Schema=1,false);
        check("unknown_profile",b=>b.SigningProfile.Profile="other",false);
        check("jks_fallback",b=>b.SigningProfile.StoreType="JKS",false);
        check("wrong_alias",b=>b.SigningProfile.Alias="other",false);
        check("uppercase_alias",b=>{b.Alias="Upper";b.SigningProfile.Alias="Upper";},false);
        check("wrong_certificate",b=>b.SigningProfile.CertificateSha256=new string('e',64),false);
        check("upload_equals_play_signer",b=>b.SigningProfile.PlayAppSigningCertificateSha256=b.CertificateSha256,false);
        check("invalid_hash",b=>b.SigningProfile.KeystoreSha256="not-a-sha",false);
        check("key_outside_private_root",b=>b.SigningProfile.Keystore=@"D:\synthetic\other\upload.p12",false);
        check("key_inside_checkout",b=>{b.SigningProfile.AllowedRoot=@"C:\synthetic\checkout\keys";b.SigningProfile.Keystore=@"C:\synthetic\checkout\keys\upload.p12";},false);
        check("ancestor_private_root",b=>{b.Checkout=@"D:\synthetic\private\checkout";},false);
        check("broad_drive_root",b=>{b.SigningProfile.AllowedRoot=@"D:\synthetic";b.SigningProfile.Keystore=@"D:\synthetic\upload.p12";},false);
        check("ads_path",b=>b.SigningProfile.ActivationEvidence=@"D:\synthetic\evidence\active.json:extra",false);
        check("unc_path",b=>b.SigningProfile.ActivationEvidence=@"\\server\share\active.json",false);
        check("reserved_name",b=>b.SigningProfile.ActivationEvidence=@"D:\synthetic\evidence\CON.json",false);
        check("wrong_extension",b=>b.SigningProfile.Keystore=@"D:\synthetic\private\upload.jks",false);
        var legacy=Binding();legacy.Schema=1;legacy.SigningProfile=null;
        if(SigningLauncher.ExternalSigning(legacy))throw new InvalidOperationException("Legacy route changed");
        rows.Add(new{Case="legacy_unchanged",Passed=true,ExternalAccepted=false});
        string parent=Path.Combine(evidence,"locked-synthetic-parent"),moved=parent+"-moved";
        Directory.CreateDirectory(parent);bool blocked=false;
        using(var held=LauncherFiles.Directory(parent)) {
            try{Directory.Move(parent,moved);}catch(IOException){blocked=true;}
        }
        if(!blocked||!Directory.Exists(parent)||Directory.Exists(moved))throw new InvalidOperationException("Ancestor lock failed");
        rows.Add(new{Case="held_directory_cannot_be_renamed",Passed=true,ExternalAccepted=false});
        return JsonSerializer.Serialize(new{Passed=true,Cases=rows,ActualPromptCalls=0,OperatingKeyUses=0});
    }
}
