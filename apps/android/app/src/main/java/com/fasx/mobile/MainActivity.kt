package com.fasx.mobile

import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.time.Instant
import java.util.concurrent.Executors

class MainActivity : ComponentActivity() {
    private val networkExecutor = Executors.newSingleThreadExecutor()
    private var uiStatus by mutableStateOf("No computer paired.")
    private var pendingInvite by mutableStateOf<PairingInvite?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            FasxTheme {
                FasxHome(
                    statusText = uiStatus,
                    pendingInvite = pendingInvite,
                    onScanInvitation = { scanInvitation() },
                    onConfirmPairing = { invite ->
                        pendingInvite = null
                        submitPairingClaim(invite)
                    },
                    onCancelPairing = { pendingInvite = null }
                )
            }
        }
    }

    private fun scanInvitation() {
        GmsBarcodeScanning.getClient(this)
            .startScan()
            .addOnSuccessListener { barcode ->
                runCatching {
                    PairingInviteParser.parse(
                        barcode.rawValue ?: error("The QR code is empty.")
                    )
                }.onSuccess { invite ->
                    pendingInvite = invite
                    uiStatus = "Invitation scanned. Review the computer address before continuing."
                }.onFailure { error ->
                    uiStatus = "Invitation not accepted: " + (error.message ?: "Invalid QR code.")
                    Toast.makeText(this, uiStatus, Toast.LENGTH_LONG).show()
                }
            }
            .addOnFailureListener { error ->
                uiStatus = "FASX could not scan the invitation: " + (error.message ?: "Unknown error")
                Toast.makeText(this, uiStatus, Toast.LENGTH_LONG).show()
            }
    }

    private fun submitPairingClaim(invite: PairingInvite) {
        uiStatus = "Connecting securely to " + invite.endpoint.host + "..."
        networkExecutor.execute {
            try {
                check(Instant.now().isBefore(invite.expiresAt)) {
                    "This FASX invitation has expired."
                }

                val proof = DeviceIdentity.createProof(this, invite)
                val client: OkHttpClient = PinnedHttpClient.create(invite)
                val claimJson = JSONObject()
                    .put("pairingId", invite.pairingId)
                    .put("secret", invite.secret)
                    .put("deviceId", proof.deviceId)
                    .put("deviceName", proof.deviceName)
                    .put("publicKeySpki", proof.publicKeySpki)
                    .put("proofNonce", proof.proofNonce)
                    .put("proofSignature", proof.proofSignature)

                val requestBody = claimJson.toString()
                    .toRequestBody("application/json; charset=utf-8".toMediaType())
                val claimRequest = Request.Builder()
                    .url(invite.endpoint.toString())
                    .post(requestBody)
                    .build()

                client.newCall(claimRequest).execute().use { response ->
                    if (response.code != 202) {
                        val errorText = response.body?.string().orEmpty()
                        val serverMessage = runCatching {
                            JSONObject(errorText).optString("error")
                        }.getOrNull().orEmpty()

                        throw IllegalStateException(
                            serverMessage.ifBlank {
                                "The Windows companion rejected the pairing request (HTTP ${response.code})."
                            }
                        )
                    }
                }

                updateUiStatus("Request sent. Approve this phone on the Windows computer.")
                val statusUrl = invite.endpoint.resolve(
                    "/api/pairing/status/${invite.pairingId}"
                ).toString()

                while (Instant.now().isBefore(invite.expiresAt)) {
                    val statusRequest = Request.Builder().url(statusUrl).get().build()
                    val status = client.newCall(statusRequest).execute().use { response ->
                        if (!response.isSuccessful) {
                            throw IllegalStateException(
                                "Could not read pairing status (HTTP ${response.code})."
                            )
                        }
                        JSONObject(response.body?.string().orEmpty()).optString("status")
                    }

                    when (status) {
                        "AwaitingLocalApproval" ->
                            updateUiStatus("Waiting for you to approve this phone on the Windows computer.")
                        "Approved" -> {
                            updateUiStatus("Paired with " + proof.deviceName + ". Remote desktop control is the next FASX milestone.")
                            return@execute
                        }
                        "Denied" -> {
                            updateUiStatus("The pairing request was denied on the Windows computer.")
                            return@execute
                        }
                        "Cancelled" -> {
                            updateUiStatus("Pairing was stopped on the Windows computer.")
                            return@execute
                        }
                        "Expired" -> {
                            updateUiStatus("The pairing invitation expired. Scan a new invitation.")
                            return@execute
                        }
                        "NotFound" ->
                            throw IllegalStateException("The pairing invitation is no longer available.")
                    }

                    Thread.sleep(1000)
                }

                updateUiStatus("The pairing invitation expired before approval.")
            } catch (error: Exception) {
                updateUiStatus("Pairing failed: " + (error.message ?: "Unknown error"))
            }
        }
    }

    private fun updateUiStatus(message: String) {
        runOnUiThread { uiStatus = message }
    }

    override fun onDestroy() {
        networkExecutor.shutdownNow()
        super.onDestroy()
    }
}

@Composable
private fun FasxTheme(content: @Composable () -> Unit) {
    val colors = darkColorScheme(
        primary = Color(0xFF6FA2FF),
        secondary = Color(0xFF8BB6FF),
        background = Color(0xFF080D18),
        surface = Color(0xFF141D30),
        onBackground = Color(0xFFF3F6FF),
        onSurface = Color(0xFFF3F6FF)
    )
    MaterialTheme(colorScheme = colors, content = content)
}

private data class DeckModule(val title: String, val description: String)

@Composable
private fun FasxHome(
    statusText: String,
    pendingInvite: PairingInvite?,
    onScanInvitation: () -> Unit,
    onConfirmPairing: (PairingInvite) -> Unit,
    onCancelPairing: () -> Unit
) {
    val modules = listOf(
        DeckModule("Remote Desktop", "View and control an approved computer"),
        DeckModule("Touchpad", "Use your phone as a mouse"),
        DeckModule("File Bridge", "Move files between paired devices"),
        DeckModule("PC Health", "Understand device status and performance"),
        DeckModule("SmartDeck", "Build a command centre that suits you"),
        DeckModule("FASX Flows", "Create approved one-tap workflows")
    )

    if (pendingInvite != null) {
        AlertDialog(
            onDismissRequest = onCancelPairing,
            title = { Text("Confirm this computer") },
            text = {
                Text(
                    "FASX will send a one-time pairing request to " +
                        pendingInvite.endpoint.host + ":" + pendingInvite.endpoint.port +
                        ". Continue only if this QR code is displayed by the Windows computer you intend to pair."
                )
            },
            confirmButton = {
                TextButton(onClick = { onConfirmPairing(pendingInvite) }) {
                    Text("Send pairing request")
                }
            },
            dismissButton = {
                TextButton(onClick = onCancelPairing) {
                    Text("Cancel")
                }
            }
        )
    }

    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 20.dp, vertical = 24.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column {
                    Text("FASX", fontSize = 30.sp, fontWeight = FontWeight.ExtraBold)
                    Text(
                        "YOUR COMPUTER. YOUR COMMAND CENTRE.",
                        fontSize = 9.sp,
                        color = Color(0xFF9CAAC4)
                    )
                }
                Text(
                    if (statusText.startsWith("Paired with")) "PAIRED" else "DEVICE STATUS",
                    color = if (statusText.startsWith("Paired with")) Color(0xFF79D6A4) else Color(0xFFFFC66D),
                    fontSize = 10.sp,
                    fontWeight = FontWeight.Bold
                )
            }

            Card(
                colors = CardDefaults.cardColors(containerColor = Color(0xFF182744)),
                shape = RoundedCornerShape(22.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Text("Connect a computer", fontSize = 21.sp, fontWeight = FontWeight.Bold)
                    Text(
                        "Pair your phone with a Windows computer on the same local network.",
                        color = Color(0xFFB6C2D8),
                        fontSize = 13.sp
                    )
                    Button(
                        onClick = onScanInvitation,
                        modifier = Modifier.fillMaxWidth(),
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF3978E8)),
                        contentPadding = PaddingValues(vertical = 13.dp)
                    ) {
                        Text("Scan FASX invitation", fontWeight = FontWeight.SemiBold)
                    }
                    Text(statusText, color = Color(0xFFCBD7EB), fontSize = 12.sp)
                }
            }

            Text(
                "YOUR SMARTDECK",
                fontSize = 11.sp,
                fontWeight = FontWeight.Bold,
                color = Color(0xFF9CAAC4)
            )
            LazyVerticalGrid(
                columns = GridCells.Fixed(2),
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(12.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                items(modules) { module ->
                    Card(
                        colors = CardDefaults.cardColors(containerColor = Color(0xFF141D30)),
                        shape = RoundedCornerShape(18.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(15.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Text(module.title, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Text(module.description, color = Color(0xFF9CAAC4), fontSize = 11.sp)
                            Text("Awaiting remote-session milestone", color = Color(0xFF7794C7), fontSize = 10.sp)
                        }
                    }
                }
            }

            Text(
                "LAN pairing preview • Remote control is not active in this build.",
                color = Color(0xFF8190AA),
                fontSize = 10.sp
            )
        }
    }
}
