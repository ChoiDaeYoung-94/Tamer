import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.cert.CertificateFactory;
import java.security.cert.X509Certificate;
import java.util.Arrays;

/** Private stdin-only worker. No files, network, key export, builds, or retries. */
public final class OperatingKeyPreflight {
    static byte[] bytes(DataInputStream in, int maximum) throws IOException {
        int length = in.readInt();
        if (length < 1 || length > maximum) throw new IOException();
        byte[] result = new byte[length];
        try {
            in.readFully(result);
            return result;
        } catch (IOException e) { clear(result); throw e; }
    }
    static char[] password(DataInputStream in) throws IOException {
        int length = in.readInt();
        if (length < 1 || length > 1024) throw new IOException();
        char[] result = new char[length];
        try {
            for (int i=0; i<length; i++) result[i] = in.readChar();
            return result;
        } catch (IOException e) { Arrays.fill(result, '\0'); throw e; }
    }
    static void clear(byte[] value) { if (value != null) Arrays.fill(value, (byte)0); }
    static void clear(char[] value) { if (value != null) Arrays.fill(value, '\0'); }

    static void verify(byte[] jks, byte[] der, String alias, char[] store, char[] keyPassword)
            throws Exception {
        if (jks.length < 8 || jks[0] != (byte)0xfe || jks[1] != (byte)0xed ||
                jks[2] != (byte)0xfe || jks[3] != (byte)0xed) throw new KeyStoreException();
        // Non-null password is mandatory: load validates JKS integrity.
        KeyStore keyStore = KeyStore.getInstance("JKS");
        keyStore.load(new ByteArrayInputStream(jks), store);
        if (!keyStore.isKeyEntry(alias)) throw new KeyStoreException();
        ByteArrayInputStream certificateInput = new ByteArrayInputStream(der);
        X509Certificate expected = (X509Certificate)CertificateFactory.getInstance("X.509")
                .generateCertificate(certificateInput);
        if (certificateInput.available() != 0 || !Arrays.equals(expected.getEncoded(), der))
            throw new java.security.cert.CertificateException();
        if (!Arrays.equals(keyStore.getCertificate(alias).getEncoded(), der))
            throw new java.security.cert.CertificateException();
        Key candidate = keyStore.getKey(alias, keyPassword);
        if (!(candidate instanceof PrivateKey privateKey)) throw new InvalidKeyException();
        byte[] challenge = new byte[64], signed = null;
        try {
            new SecureRandom().nextBytes(challenge);
            String algorithm = switch (privateKey.getAlgorithm()) {
                case "RSA" -> "SHA256withRSA";
                case "EC" -> "SHA256withECDSA";
                default -> throw new InvalidKeyException();
            };
            Signature signer = Signature.getInstance(algorithm);
            signer.initSign(privateKey);
            signer.update(challenge);
            signed = signer.sign();
            Signature verifier = Signature.getInstance(algorithm);
            verifier.initVerify(expected.getPublicKey());
            verifier.update(challenge);
            if (!verifier.verify(signed)) throw new SignatureException();
        } finally {
            clear(challenge); clear(signed);
            try { privateKey.destroy(); } catch (javax.security.auth.DestroyFailedException ignored) { }
        }
    }

    public static void main(String[] args) {
        byte[] jks = null, der = null, aliasBytes = null;
        char[] store = null, keyPassword = null;
        boolean verified = false;
        String exceptionType = "";
        try (DataInputStream in = new DataInputStream(System.in)) {
            if (args.length != 0 || in.readInt() != 0x544b5031) throw new IOException();
            jks = bytes(in, 16*1024*1024);
            der = bytes(in, 1024*1024);
            aliasBytes = bytes(in, 4096);
            String alias = new String(aliasBytes, StandardCharsets.UTF_8);
            if (alias.isBlank() || alias.indexOf('\0') >= 0) throw new KeyStoreException();
            store = password(in); keyPassword = password(in);
            if (in.read() != -1) throw new IOException();
            verify(jks, der, alias, store, keyPassword);
            verified = true;
        } catch (Throwable failure) {
            exceptionType = failure.getClass().getSimpleName();
            if (!exceptionType.matches("[A-Za-z0-9_]{1,100}")) exceptionType = "Throwable";
        } finally {
            clear(jks); clear(der); clear(aliasBytes); clear(store); clear(keyPassword);
        }
        System.out.print(verified ? "{\"status\":\"verified\",\"exceptionType\":\"\"}" :
                "{\"status\":\"failed\",\"exceptionType\":\"" + exceptionType + "\"}");
        System.out.flush();
        System.exit(verified ? 0 : 1);
    }
}
