package hr.krizevcibus.driver

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore("settings")

// Postavke uređaja: na koji server šalje, s kojim ključem i za koje vozilo
data class DriverSettings(
    val serverUrl: String,
    val apiKey: String,
    val vehicleId: Int,
)

object SettingsStore {
    private val SERVER = stringPreferencesKey("server_url")
    private val API_KEY = stringPreferencesKey("api_key")
    private val VEHICLE = intPreferencesKey("vehicle_id")

    // 10.0.2.2 je iz emulatora adresa tvog Maca
    const val DEFAULT_SERVER = "http://10.0.2.2"

    fun flow(context: Context): Flow<DriverSettings> =
        context.dataStore.data.map {
            DriverSettings(
                serverUrl = it[SERVER] ?: DEFAULT_SERVER,
                apiKey = it[API_KEY] ?: "",
                vehicleId = it[VEHICLE] ?: 1,
            )
        }

    suspend fun save(context: Context, settings: DriverSettings) {
        context.dataStore.edit {
            it[SERVER] = settings.serverUrl.trim().trimEnd('/')
            it[API_KEY] = settings.apiKey.trim()
            it[VEHICLE] = settings.vehicleId
        }
    }
}
