package hr.krizevcibus.driver

import android.location.Location
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.util.concurrent.TimeUnit

// Šalje poziciju na POST /api/vehicles/{id}/position
class PositionApi {
    private val client = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.SECONDS)
        .build()

    private val json = "application/json".toMediaType()

    suspend fun send(settings: DriverSettings, location: Location, direction: Direction) =
        withContext(Dispatchers.IO) {
            val body = JSONObject()
                .put("lat", location.latitude)
                .put("lng", location.longitude)
                .put("speed", if (location.hasSpeed()) location.speed * 3.6 else 0.0) // m/s -> km/h
                .put("heading", if (location.hasBearing()) location.bearing.toDouble() else 0.0)
                .put("direction", direction.api)
                .toString()
                .toRequestBody(json)

            val request = Request.Builder()
                .url("${settings.serverUrl}/api/vehicles/${settings.vehicleId}/position")
                .header("X-Api-Key", settings.apiKey)
                .post(body)
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    throw IOException(
                        when (response.code) {
                            401 -> "Krivi ključ (401)"
                            404 -> "Vozilo ${settings.vehicleId} ne postoji (404)"
                            503 -> "Ključ nije postavljen na serveru (503)"
                            else -> "Server vratio ${response.code}"
                        }
                    )
                }
            }
        }
}
