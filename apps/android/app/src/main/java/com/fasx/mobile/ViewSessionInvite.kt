package com.fasx.mobile

import android.util.Base64
import org.json.JSONObject
import java.net.URI
import java.time.Instant
import java.util.UUID

data class ViewSessionInvite(
    val sessionId: String,
    val secret: String,
    val nonce: String,
    val endpoint: URI,
    val tlsPin: String,
    val expiresAt: Instant
)

object ViewSessionInviteParser {
    fun parse(rawValue: String): ViewSessionInvite {
        require(rawValue.length in 1..2048) { "QR content has an invalid size." }

        val json = JSONObject(rawValue)
        require(json.optInt("v", -1) == 1) { "This FASX invitation version is not supported." }
        require(json.optString("kind") == "fasx-view") { "This QR code is not a FASX view invitation." }

        val sessionId = json.getString("sessionId")
        require(runCatching { UUID.fromString(sessionId) }.isSuccess) {
            "The session identifier is invalid."
        }

        val secret = json.getString("secret")
        require(decodeUrlSafe(secret).size == 32) { "The session secret is invalid." }

        val nonce = json.getString("nonce")
        require(decodeUrlSafe(nonce).size == 32) { "The session challenge is invalid." }

        val endpoint = URI(json.getString("endpoint"))
        require(endpoint.scheme.equals("https", ignoreCase = true) &&
            !endpoint.host.isNullOrBlank() &&
            endpoint.userInfo == null &&
            endpoint.fragment == null &&
            endpoint.rawQuery == null &&
            endpoint.rawPath == "/api/view/request" &&
            endpoint.port == 47834 &&
            isPrivateIpv4(endpoint.host)) {
            "The view-session endpoint is invalid for same-network pairing."
        }

        val tlsPin = json.getString("tlsPin")
        require(tlsPin.startsWith("sha256/")) { "The host public-key pin is missing." }
        require(Base64.decode(tlsPin.removePrefix("sha256/"), Base64.DEFAULT).size == 32) {
            "The host public-key pin is invalid."
        }

        val expiresAt = Instant.parse(json.getString("expiresAtUtc"))
        val now = Instant.now()
        require(expiresAt.isAfter(now)) { "This view invitation has expired." }
        require(!expiresAt.isAfter(now.plusSeconds(300))) {
            "The view invitation expiry is outside the supported range."
        }

        return ViewSessionInvite(
            sessionId = UUID.fromString(sessionId).toString(),
            secret = secret,
            nonce = nonce,
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
