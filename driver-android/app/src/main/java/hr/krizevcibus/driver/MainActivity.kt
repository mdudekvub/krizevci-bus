package hr.krizevcibus.driver

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import hr.krizevcibus.driver.ui.theme.KrizevciBusVozacTheme
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            KrizevciBusVozacTheme {
                DriverScreen()
            }
        }
    }
}

@Composable
fun DriverScreen() {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val status by ShiftState.status.collectAsStateWithLifecycle()
    val saved by SettingsStore.flow(context).collectAsStateWithLifecycle(initialValue = null)

    var server by rememberSaveable { mutableStateOf("") }
    var apiKey by rememberSaveable { mutableStateOf("") }
    var vehicleId by rememberSaveable { mutableStateOf("") }
    var message by remember { mutableStateOf<String?>(null) }

    // Kad se postavke učitaju, popuni polja (samo prvi put)
    LaunchedEffect(saved) {
        val s = saved ?: return@LaunchedEffect
        if (server.isEmpty()) {
            server = s.serverUrl
            apiKey = s.apiKey
            vehicleId = s.vehicleId.toString()
        }
    }

    // Sat za "prije X s"
    val now by produceState(System.currentTimeMillis()) {
        while (true) {
            value = System.currentTimeMillis()
            delay(1_000)
        }
    }

    val permissions = buildList {
        add(Manifest.permission.ACCESS_FINE_LOCATION)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) add(Manifest.permission.POST_NOTIFICATIONS)
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { result ->
        if (result[Manifest.permission.ACCESS_FINE_LOCATION] == true) {
            LocationService.start(context)
        } else {
            message = "Bez dozvole za lokaciju smjena ne može krenuti."
        }
    }

    fun startShift() {
        val id = vehicleId.toIntOrNull()
        if (server.isBlank() || apiKey.isBlank() || id == null) {
            message = "Upiši server, ključ i broj vozila."
            return
        }
        message = null
        scope.launch {
            SettingsStore.save(context, DriverSettings(server, apiKey, id))
            val granted = permissions.all {
                ContextCompat.checkSelfPermission(context, it) == PackageManager.PERMISSION_GRANTED
            }
            if (granted) LocationService.start(context) else permissionLauncher.launch(permissions.toTypedArray())
        }
    }

    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(20.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text("Križevci Bus · Vozač", fontSize = 24.sp, fontWeight = FontWeight.Bold)

            // Status
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        if (status.running) "Smjena je aktivna" else "Smjena nije aktivna",
                        fontSize = 18.sp,
                        fontWeight = FontWeight.SemiBold,
                    )
                    status.lastSentAt?.let {
                        Text("Zadnje slanje: prije ${((now - it) / 1000).coerceAtLeast(0)} s")
                    }
                    status.accuracyMeters?.let { Text("Točnost GPS-a: ${it.toInt()} m") }
                    status.lastError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                }
            }

            // Smjer
            Text("Smjer", fontWeight = FontWeight.SemiBold)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Direction.entries.forEach { d ->
                    FilterChip(
                        selected = status.direction == d,
                        onClick = { ShiftState.status.update { it.copy(direction = d) } },
                        label = { Text(d.label) },
                    )
                }
            }

            // Kreni / Završi
            if (status.running) {
                Button(
                    onClick = { LocationService.stop(context) },
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                    modifier = Modifier.fillMaxWidth().height(72.dp),
                ) { Text("Završi smjenu", fontSize = 20.sp) }
            } else {
                Button(
                    onClick = { startShift() },
                    modifier = Modifier.fillMaxWidth().height(72.dp),
                ) { Text("Kreni", fontSize = 20.sp) }
            }

            message?.let { Text(it, color = MaterialTheme.colorScheme.error) }

            // Postavke uređaja (zaključane dok traje smjena)
            Text("Postavke uređaja", fontWeight = FontWeight.SemiBold)
            OutlinedTextField(
                value = server, onValueChange = { server = it },
                label = { Text("Server") }, singleLine = true, enabled = !status.running,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = apiKey, onValueChange = { apiKey = it },
                label = { Text("Ključ") }, singleLine = true, enabled = !status.running,
                visualTransformation = PasswordVisualTransformation(),
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = vehicleId, onValueChange = { vehicleId = it.filter(Char::isDigit) },
                label = { Text("Broj vozila (ID)") }, singleLine = true, enabled = !status.running,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedButton(
                onClick = {
                    val id = vehicleId.toIntOrNull() ?: return@OutlinedButton
                    scope.launch { SettingsStore.save(context, DriverSettings(server, apiKey, id)) }
                    message = "Postavke spremljene."
                },
                enabled = !status.running,
            ) { Text("Spremi postavke") }
        }
    }
}
