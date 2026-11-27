package com.fasx.mobile

import android.content.Context
import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyPair
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.util.UUID

data class PairingDeviceProof(
    val deviceId: String,
    val deviceName: String,
    val publicKeySpki: String,
    val proofNonce: String,
    val proofSignature: String
)

object DeviceIdentity {
    private const val KEY_ALIAS = "fasx-device-signing-key-v1"
    private const val PREFS_NAME = "fasx-device-identity"
    private const val DEVICE_ID_KEY = "device-id"

    fun getOrCreateDeviceId(context: Context): String {
        val preferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val existing = preferences.getString(DEVICE_ID_KEY, null)
        if (existing != null) return existing

        val created = UUID.randomUUID().toString()
        check(preferences.edit().putString(DEVICE_ID_KEY, created).commit()) {
            "Could not save the FASX device identity."
        }
        return created
    }

    fun createProof(context: Context, invite: PairingInvite): PairingDeviceProof {
        val keyPair = getOrCreateSigningKey()
        val deviceId = getOrCreateDeviceId(context)
        val nonceBytes = ByteArray(32).also(java.security.SecureRandom()::nextBytes)
        val nonce = encodeUrlSafe(nonceBytes)
        val canonical = listOf(
            "FASX-PAIRING-V1",
            UUID.fromString(invite.pairingId).toString(),
            UUID.fromString(deviceId).toString(),
            nonce
        ).joinToString("\n").toByteArray(Charsets.UTF_8)

        val signer = Signature.getInstance("SHA256withECDSA")
        signer.initSign(keyPair.private)
        signer.update(canonical)
        val signature = signer.sign()

        return PairingDeviceProof(
            deviceId = deviceId,
            deviceName = (Build.MANUFACTURER + " " + Build.MODEL).trim().take(64),
            publicKeySpki = encodeUrlSafe(keyPair.public.encoded),
            proofNonce = nonce,
            proofSignature = encodeUrlSafe(signature)
        )
    }

    private fun getOrCreateSigningKey(): KeyPair {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        if (keyStore.containsAlias(KEY_ALIAS)) {
            val certificate = keyStore.getCertificate(KEY_ALIAS)
                ?: error("The FASX device certificate is missing.")
            val privateKey = keyStore.getKey(KEY_ALIAS, null) as? java.security.PrivateKey
                ?: error("The FASX device signing key is unavailable.")
            return KeyPair(certificate.publicKey, privateKey)
        }

        val generator = KeyPairGenerator.getInstance(
            KeyProperties.KEY_ALGORITHM_EC,
            "AndroidKeyStore"
        )
        generator.initialize(
            KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_SIGN
            )
                .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))
                .setDigests(KeyProperties.DIGEST_SHA256)
                .build()
        )
        return generator.generateKeyPair()
    }

    private fun encodeUrlSafe(value: ByteArray): String =
        Base64.encodeToString(
            value,
            Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP
        )
}
