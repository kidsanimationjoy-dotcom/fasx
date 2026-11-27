package com.fasx.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.Computer
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material.icons.outlined.HealthAndSafety
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Monitor
import androidx.compose.material.icons.outlined.Security
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.TouchApp
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch

private val FasxBackground = Color(0xFF080E1C)
private val FasxPanel = Color(0xFF131D31)
private val FasxPanelRaised = Color(0xFF1A2942)
private val FasxBlue = Color(0xFF5B94FF)
private val FasxText = Color(0xFFF2F6FF)
private val FasxMuted = Color(0xFF9EACC5)
private val FasxGreen = Color(0xFF56D6A2)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme(
                colorScheme = darkColorScheme(
                    primary = FasxBlue,
                    onPrimary = Color.White,
                    background = FasxBackground,
                    surface = FasxPanel,
                    onSurface = FasxText,
                    secondary = FasxGreen
                )
            ) {
                FasxApp()
            }
        }
    }
}

private enum class FasxTab(val label: String) {
    HOME("Home"),
    DEVICES("Devices"),
    SECURITY("Security")
}

@Composable
private fun FasxApp() {
    var selectedTab by remember { mutableStateOf(FasxTab.HOME) }
    var showPairingInfo by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    Scaffold(
        containerColor = FasxBackground,
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = {
            NavigationBar(containerColor = FasxPanel) {
                NavigationBarItem(
                    selected = selectedTab == FasxTab.HOME,
                    onClick = { selectedTab = FasxTab.HOME },
                    icon = { androidx.compose.material3.Icon(Icons.Outlined.Home, contentDescription = null) },
                    label = { Text("Home") }
                )
                NavigationBarItem(
                    selected = selectedTab == FasxTab.DEVICES,
                    onClick = { selectedTab = FasxTab.DEVICES },
                    icon = { androidx.compose.material3.Icon(Icons.Outlined.Computer, contentDescription = null) },
                    label = { Text("Devices") }
                )
                NavigationBarItem(
                    selected = selectedTab == FasxTab.SECURITY,
                    onClick = { selectedTab = FasxTab.SECURITY },
                    icon = { androidx.compose.material3.Icon(Icons.Outlined.Security, contentDescription = null) },
                    label = { Text("Security") }
                )
            }
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 18.dp)
        ) {
            Header()
            Spacer(Modifier.height(22.dp))
            ConnectionCard(onPair = { showPairingInfo = true })
            Spacer(Modifier.height(24.dp))

            when (selectedTab) {
                FasxTab.HOME -> HomeDashboard { module ->
                    scope.launch {
                        snackbarHostState.showSnackbar(
                            "$module is planned for a later connected build. No remote action was sent."
                        )
                    }
                }
                FasxTab.DEVICES -> DevicesScreen(onPair = { showPairingInfo = true })
                FasxTab.SECURITY -> SecurityScreen()
            }

            Spacer(Modifier.height(18.dp))
            Text(
                "FASX FOUNDATION PREVIEW · NO REMOTE CONNECTION ACTIVE",
                color = FasxMuted,
                fontSize = 10.sp,
                letterSpacing = 0.8.sp,
                fontWeight = FontWeight.SemiBold
            )
        }
    }

    if (showPairingInfo) {
        AlertDialog(
            onDismissRequest = { showPairingInfo = false },
            title = { Text("Pair a computer") },
            text = {
                Text(
                    "The phone interface is ready for the pairing flow, but the secure signaling service and QR scanner are not connected yet. This preview will not pretend that a computer has been paired."
                )
            },
            confirmButton = {
                TextButton(onClick = { showPairingInfo = false }) {
                    Text("Got it")
                }
            },
            containerColor = FasxPanel,
            titleContentColor = FasxText,
            textContentColor = FasxMuted
        )
    }
}

@Composable
private fun Header() {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Surface(
            modifier = Modifier.size(48.dp),
            shape = RoundedCornerShape(15.dp),
            color = FasxBlue
        ) {
            androidx.compose.foundation.layout.Box(contentAlignment = Alignment.Center) {
                Text("F", fontSize = 29.sp, fontWeight = FontWeight.Black, color = Color.White)
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text("FASX", color = FasxText, fontSize = 25.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.2.sp)
            Text("YOUR COMPUTER. YOUR COMMAND CENTRE.", color = FasxMuted, fontSize = 9.sp, letterSpacing = 0.6.sp)
        }
        androidx.compose.material3.Icon(
            Icons.Outlined.Settings,
            contentDescription = "Settings",
            tint = FasxMuted,
            modifier = Modifier.size(23.dp)
        )
    }
}

@Composable
private fun ConnectionCard(onPair: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = FasxPanel)
    ) {
        Column(modifier = Modifier.padding(19.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(shape = CircleShape, color = Color(0xFF344158), modifier = Modifier.size(9.dp)) {}
                Spacer(Modifier.width(8.dp))
                Text("NO COMPUTER PAIRED", color = FasxMuted, fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = 0.8.sp)
            }
            Spacer(Modifier.height(13.dp))
            Text("Connect your first computer", color = FasxText, fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(5.dp))
            Text(
                "Pair your Windows companion to unlock your personal command centre.",
                color = FasxMuted,
                fontSize = 13.sp,
                lineHeight = 19.sp
            )
            Spacer(Modifier.height(16.dp))
            Button(
                onClick = onPair,
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(13.dp),
                contentPadding = PaddingValues(vertical = 13.dp),
                colors = ButtonDefaults.buttonColors(containerColor = FasxBlue)
            ) {
                Text("Pair a computer", fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun HomeDashboard(onModuleClick: (String) -> Unit) {
    Text("SMARTDECK", color = FasxBlue, fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.2.sp)
    Spacer(Modifier.height(6.dp))
    Text("Your command centre", color = FasxText, fontSize = 23.sp, fontWeight = FontWeight.Bold)
    Text("Your controls, your layout, your way.", color = FasxMuted, fontSize = 13.sp)
    Spacer(Modifier.height(16.dp))

    ModuleRow(
        ModuleInfo("Remote Desktop", "View and control", Icons.Outlined.Monitor),
        ModuleInfo("Touchpad", "Mouse and keyboard", Icons.Outlined.TouchApp),
        onModuleClick
    )
    ModuleRow(
        ModuleInfo("File Bridge", "Move files safely", Icons.Outlined.Folder),
        ModuleInfo("PC Health", "Understand your PC", Icons.Outlined.HealthAndSafety),
        onModuleClick
    )
    ModuleRow(
        ModuleInfo("Quick Actions", "Your shortcuts", Icons.Outlined.Bolt),
        ModuleInfo("FASX Flows", "Approved workflows", Icons.Outlined.Bolt),
        onModuleClick
    )

    Spacer(Modifier.height(18.dp))
    Card(
        colors = CardDefaults.cardColors(containerColor = FasxPanelRaised),
        shape = RoundedCornerShape(18.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            androidx.compose.material3.Icon(Icons.Outlined.Computer, contentDescription = null, tint = FasxBlue, modifier = Modifier.size(26.dp))
            Spacer(Modifier.width(12.dp))
            Column {
                Text("SmartDeck is ready to grow", color = FasxText, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                Text("These cards are interface previews. Pairing and controls are not connected yet.", color = FasxMuted, fontSize = 11.sp, lineHeight = 16.sp)
            }
        }
    }
}

private data class ModuleInfo(val title: String, val subtitle: String, val icon: ImageVector)

@Composable
private fun ModuleRow(first: ModuleInfo, second: ModuleInfo, onClick: (String) -> Unit) {
    Row(horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxWidth()) {
        ModuleCard(first, Modifier.weight(1f), onClick)
        ModuleCard(second, Modifier.weight(1f), onClick)
    }
    Spacer(Modifier.height(12.dp))
}

@Composable
private fun ModuleCard(info: ModuleInfo, modifier: Modifier, onClick: (String) -> Unit) {
    Card(
        modifier = modifier.clickable { onClick(info.title) },
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = FasxPanel)
    ) {
        Column(modifier = Modifier.padding(15.dp).height(116.dp)) {
            androidx.compose.material3.Icon(info.icon, contentDescription = null, tint = FasxBlue, modifier = Modifier.size(24.dp))
            Spacer(Modifier.height(12.dp))
            Text(info.title, color = FasxText, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
            Text(info.subtitle, color = FasxMuted, fontSize = 10.sp)
            Spacer(Modifier.weight(1f))
            Text("COMING SOON", color = FasxMuted, fontSize = 8.sp, letterSpacing = 0.6.sp)
        }
    }
}

@Composable
private fun DevicesScreen(onPair: () -> Unit) {
    Text("YOUR DEVICES", color = FasxBlue, fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.2.sp)
    Spacer(Modifier.height(8.dp))
    Text("Device library", color = FasxText, fontSize = 23.sp, fontWeight = FontWeight.Bold)
    Spacer(Modifier.height(7.dp))
    Text("Paired computers will appear here with their connection and approval status.", color = FasxMuted, fontSize = 13.sp, lineHeight = 19.sp)
    Spacer(Modifier.height(18.dp))
    Card(colors = CardDefaults.cardColors(containerColor = FasxPanel), shape = RoundedCornerShape(18.dp)) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            androidx.compose.material3.Icon(Icons.Outlined.Computer, contentDescription = null, tint = FasxMuted, modifier = Modifier.size(34.dp))
            Spacer(Modifier.height(10.dp))
            Text("No paired computers", color = FasxText, fontWeight = FontWeight.SemiBold)
            Text("Your device list is currently empty.", color = FasxMuted, fontSize = 12.sp)
            Spacer(Modifier.height(15.dp))
            Button(onClick = onPair, shape = RoundedCornerShape(12.dp)) {
                Text("Start pairing")
            }
        }
    }
}

@Composable
private fun SecurityScreen() {
    Text("SECURITY SHIELD", color = FasxBlue, fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.2.sp)
    Spacer(Modifier.height(8.dp))
    Text("Your control stays yours", color = FasxText, fontSize = 23.sp, fontWeight = FontWeight.Bold)
    Spacer(Modifier.height(7.dp))
    Text("FASX is designed around clear consent and independent permissions.", color = FasxMuted, fontSize = 13.sp, lineHeight = 19.sp)
    Spacer(Modifier.height(17.dp))
    SecurityItem("Local approval", "A new phone must be approved on the Windows computer.")
    SecurityItem("Separate permissions", "Screen viewing, input, files and power actions are separate grants.")
    SecurityItem("Visible sessions", "The Windows companion will show when a remote session is active.")
    SecurityItem("Emergency disconnect", "The local computer owner can end a session immediately.")
}

@Composable
private fun SecurityItem(title: String, description: String) {
    Card(
        modifier = Modifier.fillMaxWidth().padding(bottom = 10.dp),
        colors = CardDefaults.cardColors(containerColor = FasxPanel),
        shape = RoundedCornerShape(16.dp)
    ) {
        Row(modifier = Modifier.padding(15.dp), verticalAlignment = Alignment.Top) {
            Surface(shape = CircleShape, color = Color(0xFF193F3A), modifier = Modifier.size(30.dp)) {
                androidx.compose.foundation.layout.Box(contentAlignment = Alignment.Center) {
                    androidx.compose.material3.Icon(Icons.Outlined.Security, contentDescription = null, tint = FasxGreen, modifier = Modifier.size(16.dp))
                }
            }
            Spacer(Modifier.width(11.dp))
            Column {
                Text(title, color = FasxText, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                Text(description, color = FasxMuted, fontSize = 11.sp, lineHeight = 16.sp)
            }
        }
    }
}
