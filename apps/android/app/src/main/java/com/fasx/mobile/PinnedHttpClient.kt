package com.fasx.mobile

import android.util.Base64
import okhttp3.OkHttpClient
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import javax.net.ssl.SSLContext
import javax.net.ssl.X509TrustManager

/**
 * Trusts only the public key fingerprint printed into the one-time FASX invitation QR.
 * The platform's default hostname verifier is retained, so the certificate must also
 * contain the endpoint IP in its subject alternative names.
 */
object PinnedHttpClient {
    fun create(invite: PairingInvite): OkHttpClient {
        val expectedPin = Base64.decode(
            invite.tlsPin.removePrefix("sha256/"),
            Base64.DEFAULT
        )

        val trustManager = object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) {
                throw CertificateException("FASX pairing server does not request client certificates.")
            }

            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                if (chain.size != 1) {
                    throw CertificateException("Unexpected FASX host certificate chain.")
                }

                val certificate = chain[0]
                certificate.checkValidity()
                certificate.verify(certificate.publicKey)

                val actualPin = MessageDigest.getInstance("SHA-256")
                    .digest(certificate.publicKey.encoded)

                if (!MessageDigest.isEqual(expectedPin, actualPin)) {
                    throw CertificateException("The FASX host certificate does not match the scanned invitation.")
                }
            }

            override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
        }

        val sslContext = SSLContext.getInstance("TLS").apply {
            init(null, arrayOf(trustManager), SecureRandom())
        }

        return OkHttpClient.Builder()
            .sslSocketFactory(sslContext.socketFactory, trustManager)
            .build()
    }
}
