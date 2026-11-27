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
        require(!endpoint.host.isNullOrBlank() &&
            endpoint.userInfo == null &&
            endpoint.fragment == null &&
            endpoint.rawQuery == null &&
            endpoint.rawPath == "/api/pairing/claim") {
            "The pairing endpoint is invalid."
        }
        require(endpoint.port == 47832) { "The pairing endpoint port is not supported." }
        require(isPrivateIpv4(endpoint.host)) {
            "This foundation build supports same-network private IPv4 pairing only."
        }

        val tlsPin = json.getString("tlsPin")
        require(tlsPin.startsWith("sha256/")) { "The host public-key pin is missing." }
        val pinBytes = Base64.decode(tlsPin.removePrefix("sha256/"), Base64.DEFAULT)
        require(pinBytes.size == 32) { "The host public-key pin is invalid." }

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

    private fun isPrivateIpv4(host: String): Boolean {
        val parts = host.split(".")
        if (parts.size != 4) return false

        val octets = parts.map { part ->
            if (part.isEmpty() || part.length > 3 || (part.length > 1 && part.startsWith("0"))) {
                return false
            }
            part.toIntOrNull()?.takeIf { it in 0..255 } ?: return false
        }

        return octets[0] == 10 ||
            (octets[0] == 172 && octets[1] in 16..31) ||
            (octets[0] == 192 && octets[1] == 168)
    }

    private fun decodeUrlSafe(value: String): ByteArray =
        Base64.decode(value, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
}
