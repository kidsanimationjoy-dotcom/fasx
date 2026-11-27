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
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            FasxTheme {
                FasxHome(onScanInvitation = { scanInvitation() })
            }
        }
    }

    private fun scanInvitation() {
        GmsBarcodeScanning.getClient(this)
            .startScan()
            .addOnSuccessListener { barcode ->
                val message = runCatching {
                    val invite = PairingInviteParser.parse(
                        barcode.rawValue ?: error("The QR code is empty.")
                    )
                    val proof = DeviceIdentity.createProof(this, invite)
                    "Invitation checked for " + proof.deviceName +
                        ". A device-key proof was created locally. The secure host connection is not available in this foundation build yet."
                }.getOrElse { error ->
                    "Invitation not accepted: " + (error.message ?: "Invalid QR code.")
                }
                Toast.makeText(this, message, Toast.LENGTH_LONG).show()
            }
            .addOnFailureListener { error ->
                Toast.makeText(
                    this,
                    "FASX could not scan the invitation: " + (error.message ?: "Unknown error"),
                    Toast.LENGTH_LONG
                ).show()
            }
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
private fun FasxHome(onScanInvitation: () -> Unit) {
    val modules = listOf(
        DeckModule("Remote Desktop", "View and control an approved computer"),
        DeckModule("Touchpad", "Use your phone as a mouse"),
        DeckModule("File Bridge", "Move files between paired devices"),
        DeckModule("PC Health", "Understand device status and performance"),
        DeckModule("SmartDeck", "Build a command centre that suits you"),
        DeckModule("FASX Flows", "Create approved one-tap workflows")
    )

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
                    "NOT PAIRED",
                    color = Color(0xFFFFC66D),
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
                        "Pair your phone with a Windows computer after the host companion creates a secure invitation.",
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
                            Text("Awaiting connection", color = Color(0xFF7794C7), fontSize = 10.sp)
                        }
                    }
                }
            }

            Text(
                "Foundation preview • Remote control is not active in this build.",
                color = Color(0xFF8190AA),
                fontSize = 10.sp
            )
        }
    }
}
