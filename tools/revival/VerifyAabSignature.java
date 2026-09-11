import java.io.InputStream;
import java.io.OutputStream;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.security.cert.X509Certificate;
import java.util.HashSet;
import java.util.HexFormat;
import java.util.jar.JarFile;

/** Verify every AAB payload entry through Java's verifying JarFile reader.
 * AGP may place MANIFEST.MF last, which JarInputStream cannot verify in order.
 * This verifies random-access payload coverage instead of ignoring that warning.
 */
public class VerifyAabSignature {
    public static void main(String[] args) throws Exception {
        if (args.length != 1 && args.length != 3) throw new IllegalArgumentException("Expected AAB path and optional --release-cert-sha256 digest");
        String expectedRelease = null;
        if (args.length == 3) {
            if (!args[1].equals("--release-cert-sha256") || !args[2].matches("[0-9a-fA-F]{64}"))
                throw new IllegalArgumentException("Expected explicit release certificate SHA-256");
            expectedRelease = args[2].toLowerCase();
        }
        int count = 0;
        String signer = null;
        var names = new HashSet<String>();
        try (var jar = new JarFile(Path.of(args[0]).toFile(), true)) {
            var entries = jar.entries();
            while (entries.hasMoreElements()) {
                var entry = entries.nextElement();
                if (!names.add(entry.getName())) throw new SecurityException("Duplicate entry");
                if (entry.isDirectory()) continue;
                try (InputStream stream = jar.getInputStream(entry)) {
                    stream.transferTo(OutputStream.nullOutputStream());
                }
                if (entry.getName().matches("(?i)META-INF/(MANIFEST\\.MF|[^/]+\\.(SF|RSA|DSA|EC))")) continue;
                var certificates = entry.getCertificates();
                if (certificates == null || certificates.length != 1)
                    throw new SecurityException("Unsigned payload or unexpected signer chain");
                var certificate = (X509Certificate) certificates[0];
                certificate.checkValidity();
                String digest = HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(certificate.getEncoded()));
                boolean debug = certificate.getSubjectX500Principal().getName().contains("CN=Android Debug");
                if (expectedRelease == null && !debug) throw new SecurityException("Expected debug signer");
                if (expectedRelease != null && (debug || !expectedRelease.equals(digest)))
                    throw new SecurityException("Release signer does not match reviewed certificate");
                if (signer != null && !signer.equals(digest)) throw new SecurityException("Mixed payload signers");
                signer = digest;
                count++;
            }
        }
        if (count == 0) throw new SecurityException("No verified payload entries");
        System.out.println("{\"api\":\"JarFile\",\"verifiedPayloadEntries\":" + count
            + ",\"signerSha256\":\"" + signer + "\",\"allPayloadEntriesSigned\":true}");
    }
}
