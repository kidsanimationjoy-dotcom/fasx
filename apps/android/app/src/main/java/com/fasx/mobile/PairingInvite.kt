package com.fasx.mobile

import android.util.Base64
import org.json.JSONObject
import java.net.URI
import java.time.Instant
import java.util.UUID

data class PairingInvite(
    val pairingId: String,
    val secret: String,
    val endpoint: URI,
    val tlsPin: String,
    val expiresAt: Instant
)

object PairingInviteParser {
    fun parse(rawValue: String): PairingInvite {
        require(rawValue.length in 1..2048) { "QR content has an invalid size." }

        val json = JSONObject(rawValue)
        require(json.optInt("v", -1) == 1) { "This FASX invitation version is not supported." }
        require(json.optString("kind") == "fasx-pair") { "This QR code is not a FASX pairing invitation." }

        val pairingId = json.getString("pairingId")
        require(runCatching { UUID.fromString(pairingId) }.isSuccess) {
            "The pairing identifier is invalid."
        }

        val secret = json.getString("secret")
        val secretBytes = decodeUrlSafe(secret)
        require(secretBytes.size == 32) { "The pairing secret is invalid." }

        val endpoint = URI(json.getString("endpoint"))
        require(endpoint.scheme.equals("https", ignoreCase = true)) {
            "FASX pairing requires a secure HTTPS endpoint."
        }
        require(!endpoint.host.isNullOrBlank() && endpoint.userInfo == null && endpoint.fragment == null) {
            "The pairing endpoint is invalid."
        }
        require(endpoint.port in -1..65535) { "The pairing endpoint port is invalid." }

        val tlsPin = json.getString("tlsPin")
        require(tlsPin.startsWith("sha256/")) { "The host certificate pin is missing." }
        val pinBytes = Base64.decode(tlsPin.removePrefix("sha256/"), Base64.DEFAULT)
        require(pinBytes.size == 32) { "The host certificate pin is invalid." }

        val expiresAt = Instant.parse(json.getString("expiresAtUtc"))
        val now = Instant.now()
        require(expiresAt.isAfter(now)) { "This FASX invitation has expired." }
        require(!expiresAt.isAfter(now.plusSeconds(300))) {
            "Invitation expiry is outside the supported range."
        }

        return PairingInvite(
            pairingId = UUID.fromString(pairingId).toString(),
            secret = secret,
            endpoint = endpoint,
            tlsPin = tlsPin,
            expiresAt = expiresAt
        )
    }

    private fun decodeUrlSafe(value: String): ByteArray =
        Base64.decode(value, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
}
