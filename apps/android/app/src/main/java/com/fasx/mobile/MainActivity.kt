package com.fasx.mobile

import android.net.Uri
import android.os.Bundle
import android.provider.OpenableColumns
import android.util.Base64
import android.webkit.JavascriptInterface
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.ActivityResultLauncher
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest
import java.time.Instant
import java.util.UUID
import java.util.concurrent.Executors

class MainActivity : ComponentActivity() {
    private val networkExecutor = Executors.newSingleThreadExecutor()
    private lateinit var filePickerLauncher: ActivityResultLauncher<Array<String>>
    private var uiStatus by mutableStateOf("No computer paired.")
    private var pendingInvite by mutableStateOf<PairingInvite?>(null)
    private var pendingViewInvite by mutableStateOf<ViewSessionInvite?>(null)
    private var activeViewInvite by mutableStateOf<ViewSessionInvite?>(null)
    private var viewOfferSdp by mutableStateOf<String?>(null)
    private var remoteControlAllowed by mutableStateOf(false)
    private var fileTransferAllowed by mutableStateOf(false)
    private val deckCatalog = listOf(
        DeckModule("connect", "Connect computer", "Scan and pair a Windows computer", "READY"),
        DeckModule("remote", "Remote Desktop", "Request a screen session from a paired PC", "READY"),
        DeckModule("touchpad", "Touch Control", "Use approved touch and basic keys during a session", "PERMISSION"),
        DeckModule("security", "Security Shield", "See pairing and session-safety status", "READY"),
        DeckModule("pc-health", "PC Health", "Read local diagnostics on the Windows computer", "LOCAL"),
        DeckModule("file-bridge", "File Bridge", "Send selected files to an approved Windows folder", "PERMISSION"),
        DeckModule("flows", "FASX Flows", "Run user-approved one-tap workflows", "PLANNED")
    )
    private var visibleDeckIds by mutableStateOf(deckCatalog.map { it.id })

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        filePickerLauncher = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
            if (uri != null) sendFileToComputer(uri)
        }
        val preferences = getSharedPreferences("fasx-smartdeck", MODE_PRIVATE)
        val savedDeck = preferences.getString("visible_ids", null)
            ?.split("|")
            ?.filter { id -> deckCatalog.any { it.id == id } }
            ?.distinct()
        if (savedDeck != null) visibleDeckIds = savedDeck
        setContent {
            FasxTheme {
                FasxHome(
                    statusText = uiStatus,
                    pendingInvite = pendingInvite,
                    pendingViewInvite = pendingViewInvite,
                    viewOfferSdp = viewOfferSdp,
                    controlInputAllowed = remoteControlAllowed,
                    fileTransferAllowed = fileTransferAllowed,
                    visibleDeckIds = visibleDeckIds,
                    deckModules = deckCatalog,
                    onSaveDeck = { ids ->
                        visibleDeckIds = ids.filter { id -> deckCatalog.any { it.id == id } }.distinct()
                        preferences.edit().putString("visible_ids", visibleDeckIds.joinToString("|")).apply()
                    },
                    onModuleSelected = { id ->
                        when (id) {
                            "connect" -> scanInvitation()
                            "remote" -> scanInvitation()
                            "touchpad" -> updateUiStatus(
                                if (viewOfferSdp != null && remoteControlAllowed)
                                    "Touch controls are shown below the active remote screen."
                                else
                                    "Touch controls appear only during a remote session when ControlInput is separately approved on Windows."
                            )
                            "security" -> updateUiStatus(
                                "FASX Security Shield: phone pairing and each remote session require approval on the Windows computer. Screen viewing does not automatically grant input."
                            )
                            "pc-health" -> updateUiStatus(
                                "PC Health runs locally on Windows. Open the PC Health panel on your computer to read its diagnostics."
                            )
                            "file-bridge" -> {
                                if (activeViewInvite != null && fileTransferAllowed) {
                                    filePickerLauncher.launch(arrayOf("*/*"))
                                } else {
                                    updateUiStatus(
                                        "File Bridge requires the Windows owner to approve TransferFiles and select a destination folder for this session."
                                    )
                                }
                            }
                            "flows" -> updateUiStatus(
                                "FASX Flows is planned. Actions will use registered, user-approved steps rather than arbitrary commands."
                            )
                            else -> updateUiStatus("This SmartDeck card is not available.")
                        }
                    },
                    onSubmitViewAnswer = { sdp -> submitViewSessionAnswer(sdp) },
                    onSendFile = { uri -> sendFileToComputer(uri) },
                    onRemoteViewStatus = { message -> updateUiStatus(message) },
                    onStopViewing = { stopViewing() },
                    onScanInvitation = { scanInvitation() },
                    onConfirmPairing = { invite ->
                        pendingInvite = null
                        submitPairingClaim(invite)
                    },
                    onConfirmViewSession = { invite ->
                        pendingViewInvite = null
                        submitViewSessionRequest(invite)
                    },
                    onCancelPairing = {
                        pendingInvite = null
                        pendingViewInvite = null
                    }
                )
            }
        }
    }

    private fun scanInvitation() {
        GmsBarcodeScanning.getClient(this)
            .startScan()
            .addOnSuccessListener { barcode ->
                runCatching {
                    barcode.rawValue ?: error("The QR code is empty.")
                }.onSuccess { rawValue ->
                    runCatching {
                        when (JSONObject(rawValue).optString("kind")) {
                            "fasx-pair" -> {
                                pendingInvite = PairingInviteParser.parse(rawValue)
                                uiStatus = "Pairing invitation scanned. Review the computer address."
                            }
                            "fasx-view" -> {
                                pendingViewInvite = ViewSessionInviteParser.parse(rawValue)
                                uiStatus = "View-session request scanned. Review the computer address."
                            }
                            else -> error("This QR code is not a supported FASX invitation.")
                        }
                    }.onFailure { error ->
                        uiStatus = "Invitation not accepted: " + (error.message ?: "Invalid QR code.")
                        Toast.makeText(this, uiStatus, Toast.LENGTH_LONG).show()
                    }
                }.onFailure { error ->
                    uiStatus = "FASX could not read the QR code: " + (error.message ?: "Unknown error")
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
                    val statusRequest = Request.Builder()
                        .url(statusUrl)
                        .header("X-FASX-Session-Token", invite.secret)
                        .get()
                        .build()
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

    private fun submitViewSessionRequest(invite: ViewSessionInvite) {
        uiStatus = "Authenticating this phone with " + invite.endpoint.host + "..."
        networkExecutor.execute {
            try {
                check(Instant.now().isBefore(invite.expiresAt)) {
                    "This view-session invitation has expired."
                }

                val proof = DeviceIdentity.createSessionProof(this, invite.sessionId, invite.nonce)
                val client: OkHttpClient = PinnedHttpClient.create(
                    PairingInvite(
                        pairingId = invite.sessionId,
                        secret = invite.secret,
                        endpoint = invite.endpoint,
                        tlsPin = invite.tlsPin,
                        expiresAt = invite.expiresAt
                    )
                )
                val requestJson = JSONObject()
                    .put("sessionId", proof.sessionId)
                    .put("deviceId", proof.deviceId)
                    .put("secret", invite.secret)
                    .put("nonce", proof.nonce)
                    .put("signature", proof.signature)

                val request = Request.Builder()
                    .url(invite.endpoint.toString())
                    .post(
                        requestJson.toString().toRequestBody(
                            "application/json; charset=utf-8".toMediaType()
                        )
                    )
                    .build()

                client.newCall(request).execute().use { response ->
                    if (response.code != 202) {
                        val body = response.body?.string().orEmpty()
                        val message = runCatching {
                            JSONObject(body).optString("error")
                        }.getOrNull().orEmpty()
                        throw IllegalStateException(
                            message.ifBlank {
                                "The Windows computer rejected the session request (HTTP ${response.code})."
                            }
                        )
                    }
                }

                updateUiStatus("View-only request sent. Approve View screen on the Windows computer.")
                val statusUrl = invite.endpoint.resolve(
                    "/api/view/status/${invite.sessionId}"
                ).toString()

                while (Instant.now().isBefore(invite.expiresAt.plusSeconds(600))) {
                    val statusRequest = Request.Builder()
                        .url(statusUrl)
                        .header("X-FASX-Session-Token", invite.secret)
                        .get()
                        .build()
                    val status = client.newCall(statusRequest).execute().use { response ->
                        if (!response.isSuccessful) {
                            throw IllegalStateException(
                                "Could not read view-session status (HTTP ${response.code})."
                            )
                        }
                        JSONObject(response.body?.string().orEmpty()).optString("status")
                    }

                    when (status) {
                        "AwaitingLocalApproval" ->
                            updateUiStatus("Waiting for the Windows user to approve View screen only.")
                        "Approved" -> {
                            updateUiStatus("Windows approved View screen. Waiting for the screen stream…")
                            val offerRequest = Request.Builder()
                                .url(invite.endpoint.resolve("/api/view/offer/${invite.sessionId}").toString())
                                .header("X-FASX-Session-Token", invite.secret)
                                .get()
                                .build()

                            val offerResult = client.newCall(offerRequest).execute().use { response ->
                                if (response.code == 401) {
                                    throw IllegalStateException("The view-session token was rejected.")
                                }
                                if (!response.isSuccessful) {
                                    throw IllegalStateException("Could not retrieve the screen offer (HTTP ${response.code}).")
                                }
                                JSONObject(response.body?.string().orEmpty())
                            }

                            when (offerResult.optString("status")) {
                                "OfferReady" -> {
                                    val sdp = offerResult.getString("sdp")
                                    runOnUiThread {
                                        activeViewInvite = invite
                                        viewOfferSdp = sdp
                                        remoteControlAllowed = offerResult.optBoolean("controlInputApproved", false)
                                        fileTransferAllowed = offerResult.optBoolean("fileTransferApproved", false)
                                        uiStatus = if (remoteControlAllowed) {
                                            "Screen offer received. Windows approved view and limited input."
                                        } else {
                                            "Screen offer received. Establishing a view-only connection…"
                                        }
                                    }
                                    return@execute
                                }
                                "WaitingForScreen" ->
                                    updateUiStatus("View screen is approved. Start screen sharing on the Windows computer.")
                                "Expired", "Revoked", "Cancelled" -> {
                                    updateUiStatus("The Windows view-session permission has ended.")
                                    return@execute
                                }
                                "Denied" -> {
                                    updateUiStatus("The Windows user denied the view-only request.")
                                    return@execute
                                }
                            }
                        }
                        "Denied" -> {
                            updateUiStatus("The Windows user denied the view-session request.")
                            return@execute
                        }
                        "Revoked", "Cancelled" -> {
                            updateUiStatus("The view-session authorization was stopped on Windows.")
                            return@execute
                        }
                        "Expired" -> {
                            updateUiStatus("The view-session request expired. Scan a new invitation.")
                            return@execute
                        }
                        "NotFound" ->
                            throw IllegalStateException("The view-session invitation is no longer available.")
                    }
                    Thread.sleep(1000)
                }

                updateUiStatus("The view-session approval window timed out.")
            } catch (error: Exception) {
                updateUiStatus("View-session request failed: " + (error.message ?: "Unknown error"))
            }
        }
    }

    private fun submitViewSessionAnswer(sdp: String) {
        val invite = activeViewInvite ?: return
        networkExecutor.execute {
            try {
                val client = PinnedHttpClient.create(
                    PairingInvite(
                        pairingId = invite.sessionId,
                        secret = invite.secret,
                        endpoint = invite.endpoint,
                        tlsPin = invite.tlsPin,
                        expiresAt = invite.expiresAt
                    )
                )
                val body = JSONObject()
                    .put("sessionId", invite.sessionId)
                    .put("secret", invite.secret)
                    .put("sdp", sdp)
                    .toString()
                    .toRequestBody("application/json; charset=utf-8".toMediaType())

                val request = Request.Builder()
                    .url(invite.endpoint.resolve("/api/view/answer/${invite.sessionId}").toString())
                    .post(body)
                    .build()

                client.newCall(request).execute().use { response ->
                    if (response.code != 202) {
                        val errorText = response.body?.string().orEmpty()
                        val message = runCatching {
                            JSONObject(errorText).optString("error")
                        }.getOrNull().orEmpty()
                        throw IllegalStateException(
                            message.ifBlank { "The Windows computer rejected the WebRTC answer (HTTP ${response.code})." }
                        )
                    }
                }
                updateUiStatus("Phone answer sent securely. Connecting the video stream…")
            } catch (error: Exception) {
                updateUiStatus("Could not send the view-session answer: " + (error.message ?: "Unknown error"))
            }
        }
    }

    private fun stopViewing() {
        viewOfferSdp = null
        remoteControlAllowed = false
        fileTransferAllowed = false
        activeViewInvite = null
        uiStatus = "View-only connection closed on this phone."
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

private data class DeckModule(
    val id: String,
    val title: String,
    val description: String,
    val availability: String
)

@Composable
private fun FasxHome(
    statusText: String,
    pendingInvite: PairingInvite?,
    pendingViewInvite: ViewSessionInvite?,
    viewOfferSdp: String?,
    controlInputAllowed: Boolean,
    fileTransferAllowed: Boolean,
    visibleDeckIds: List<String>,
    deckModules: List<DeckModule>,
    onSaveDeck: (List<String>) -> Unit,
    onModuleSelected: (String) -> Unit,
    onSubmitViewAnswer: (String) -> Unit,
    onSendFile: (Uri) -> Unit,
    onRemoteViewStatus: (String) -> Unit,
    onStopViewing: () -> Unit,
    onScanInvitation: () -> Unit,
    onConfirmPairing: (PairingInvite) -> Unit,
    onConfirmViewSession: (ViewSessionInvite) -> Unit,
    onCancelPairing: () -> Unit
) {
    val modules = deckModules
    val visibleModules = modules
        .filter { it.id in visibleDeckIds }
        .sortedBy { visibleDeckIds.indexOf(it.id) }
    var showDeckEditor by remember { mutableStateOf(false) }
    var draftDeckIds by remember(visibleDeckIds) { mutableStateOf(visibleDeckIds) }

    if (showDeckEditor) {
        AlertDialog(
            onDismissRequest = { showDeckEditor = false },
            title = { Text("Customize SmartDeck") },
            text = {
                Column(
                    modifier = Modifier
                        .heightIn(max = 440.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text(
                        "Choose the cards you want on your home screen. Use the arrows to change their order.",
                        color = Color(0xFF9CAAC4),
                        fontSize = 12.sp
                    )
                    modules.forEach { module ->
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Checkbox(
                                checked = module.id in draftDeckIds,
                                onCheckedChange = { checked ->
                                    draftDeckIds = if (checked) {
                                        draftDeckIds + module.id
                                    } else {
                                        draftDeckIds - module.id
                                    }
                                }
                            )
                            Column(modifier = Modifier.weight(1f)) {
                                Text(module.title, fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                                Text(module.description, color = Color(0xFF9CAAC4), fontSize = 10.sp)
                            }
                            TextButton(
                                enabled = module.id in draftDeckIds && draftDeckIds.indexOf(module.id) > 0,
                                onClick = {
                                    val mutable = draftDeckIds.toMutableList()
                                    val index = mutable.indexOf(module.id)
                                    if (index > 0) {
                                        val previous = mutable[index - 1]
                                        mutable[index - 1] = mutable[index]
                                        mutable[index] = previous
                                        draftDeckIds = mutable
                                    }
                                }
                            ) { Text("↑") }
                            TextButton(
                                enabled = module.id in draftDeckIds &&
                                    draftDeckIds.indexOf(module.id) < draftDeckIds.lastIndex,
                                onClick = {
                                    val mutable = draftDeckIds.toMutableList()
                                    val index = mutable.indexOf(module.id)
                                    if (index >= 0 && index < mutable.lastIndex) {
                                        val next = mutable[index + 1]
                                        mutable[index + 1] = mutable[index]
                                        mutable[index] = next
                                        draftDeckIds = mutable
                                    }
                                }
                            ) { Text("↓") }
                        }
                    }
                    if (draftDeckIds.isEmpty()) {
                        Text("Choose at least one card before saving.", color = Color(0xFFFFC66D), fontSize = 11.sp)
                    }
                }
            },
            confirmButton = {
                TextButton(
                    enabled = draftDeckIds.isNotEmpty(),
                    onClick = {
                        onSaveDeck(draftDeckIds)
                        showDeckEditor = false
                    }
                ) { Text("Save SmartDeck") }
            },
            dismissButton = {
                TextButton(onClick = { showDeckEditor = false }) { Text("Cancel") }
            }
        )
    }

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

    if (pendingViewInvite != null) {
        AlertDialog(
            onDismissRequest = onCancelPairing,
            title = { Text("Request view-only access") },
            text = {
                Text(
                    "This will send a signed View screen request to " +
                        pendingViewInvite.endpoint.host + ":" + pendingViewInvite.endpoint.port +
                        ". The computer owner must approve it locally." +
                        if (pendingViewInvite.controlInputRequested) {
                            " This invitation also asks to let the computer owner consider pointer and limited keyboard input. Input remains disabled unless the owner separately approves it in Windows. No shell commands, file, clipboard or power access are requested."
                        } else {
                            " This is view-only and does not request mouse, keyboard, files or clipboard access."
                        }
                )
            },
            confirmButton = {
                TextButton(onClick = { onConfirmViewSession(pendingViewInvite) }) {
                    Text(if (pendingViewInvite.controlInputRequested) "Request screen + optional input" else "Send view-only request")
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

            if (viewOfferSdp != null) {
                Card(
                    colors = CardDefaults.cardColors(containerColor = Color(0xFF141D30)),
                    shape = RoundedCornerShape(18.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(
                        modifier = Modifier.padding(12.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text("REMOTE SCREEN", fontSize = 11.sp, fontWeight = FontWeight.Bold, color = Color(0xFF9CAAC4))
                        AndroidView(
                            factory = { context ->
                                WebView(context).apply {
                                    settings.javaScriptEnabled = true
                                    settings.domStorageEnabled = false
                                    val state = RemoteViewWebState()
                                    tag = state

                                    addJavascriptInterface(object {
                                        @JavascriptInterface
                                        fun submitAnswer(sdp: String) {
                                            onSubmitViewAnswer(sdp)
                                        }

                                        @JavascriptInterface
                                        fun reportStatus(message: String) {
                                            onRemoteViewStatus(message)
                                        }
                                    }, "Fasx")

                                    webViewClient = object : WebViewClient() {
                                        override fun shouldOverrideUrlLoading(
                                            view: WebView?,
                                            request: android.webkit.WebResourceRequest
                                        ): Boolean {
                                            val requested = request.url.toString()
                                            return !requested.equals(
                                                "file:///android_asset/remote-view.html",
                                                ignoreCase = true
                                            )
                                        }

                                        override fun onPageFinished(view: WebView?, url: String?) {
                                            state.pageLoaded = true
                                            val pending = state.pendingOfferSdp
                                            if (pending != null && state.lastOfferSdp != pending) {
                                                evaluateJavascript(
                                                    "window.startFasxRemoteView(${JSONObject.quote(pending)}, ${controlInputAllowed})",
                                                    null
                                                )
                                                state.lastOfferSdp = pending
                                            }
                                        }
                                    }
                                    loadUrl("file:///android_asset/remote-view.html")
                                }
                            },
                            update = { webView ->
                                val state = webView.tag as? RemoteViewWebState
                                if (state != null) {
                                    state.pendingOfferSdp = viewOfferSdp
                                    if (state.pageLoaded &&
                                        viewOfferSdp != null &&
                                        state.lastOfferSdp != viewOfferSdp
                                    ) {
                                        webView.evaluateJavascript(
                                            "window.startFasxRemoteView(${JSONObject.quote(viewOfferSdp)}, ${controlInputAllowed})",
                                            null
                                        )
                                        state.lastOfferSdp = viewOfferSdp
                                    }
                                }
                            },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(250.dp),
                            onRelease = { webView ->
                                webView.evaluateJavascript("window.stopFasxRemoteView?.()", null)
                                webView.destroy()
                            }
                        )
                        TextButton(onClick = onStopViewing) {
                            Text("Stop viewing")
                        }
                    }
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    "YOUR SMARTDECK",
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFF9CAAC4)
                )
                TextButton(onClick = {
                    draftDeckIds = visibleDeckIds
                    showDeckEditor = true
                }) { Text("Customize") }
            }
            if (visibleModules.isEmpty()) {
                Text("Your SmartDeck is empty. Open Customize to add cards.", color = Color(0xFF9CAAC4), fontSize = 12.sp)
            }
            LazyVerticalGrid(
                columns = GridCells.Fixed(2),
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(12.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                items(visibleModules) { module ->
                    Card(
                        colors = CardDefaults.cardColors(containerColor = Color(0xFF141D30)),
                        shape = RoundedCornerShape(18.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable { onModuleSelected(module.id) }
                    ) {
                        Column(
                            modifier = Modifier.padding(15.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Text(module.title, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Text(module.description, color = Color(0xFF9CAAC4), fontSize = 11.sp)
                            Text(
                                when (module.availability) {
                                    "READY" -> "AVAILABLE • TAP TO OPEN"
                                    "PERMISSION" -> "REQUIRES SESSION PERMISSION"
                                    "LOCAL" -> "AVAILABLE ON WINDOWS"
                                    else -> "PLANNED MODULE"
                                },
                                color = when (module.availability) {
                                    "READY" -> Color(0xFF79D6A4)
                                    "PERMISSION" -> Color(0xFFFFC66D)
                                    else -> Color(0xFF7794C7)
                                },
                                fontSize = 9.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }
            }

            Text(
                "SmartDeck settings are saved on this phone. Remote actions still require session permission on Windows.",
                color = Color(0xFF8190AA),
                fontSize = 10.sp
            )
        }
    }
}

private class RemoteViewWebState {
    var pageLoaded: Boolean = false
    var pendingOfferSdp: String? = null
    var lastOfferSdp: String? = null
}
