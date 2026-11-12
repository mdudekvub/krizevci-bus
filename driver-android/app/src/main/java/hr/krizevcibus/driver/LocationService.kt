package hr.krizevcibus.driver

import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.Looper
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

// Foreground servis: radi i kad je zaslon ugašen, uz stalnu obavijest
class LocationService : Service() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val api = PositionApi()
    private lateinit var fused: FusedLocationProviderClient
    private var settings: DriverSettings? = null
    private var sending = false

    private val callback = object : LocationCallback() {
        override fun onLocationResult(result: LocationResult) {
            val location = result.lastLocation ?: return
            val s = settings ?: return
            if (sending) return // spora mreža: preskoči, ionako šaljemo samo zadnju poziciju
            sending = true

            scope.launch {
                try {
                    api.send(s, location, ShiftState.status.value.direction)
                    ShiftState.status.update {
                        it.copy(
                            lastSentAt = System.currentTimeMillis(),
                            lastError = null,
                            accuracyMeters = location.accuracy,
                        )
                    }
                } catch (e: Exception) {
                    ShiftState.status.update {
                        it.copy(lastError = e.message ?: "Greška u slanju", accuracyMeters = location.accuracy)
                    }
                } finally {
                    sending = false
                }
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        fused = LocationServices.getFusedLocationProviderClient(this)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopShift()
            return START_NOT_STICKY
        }

        startInForeground()
        ShiftState.status.update { it.copy(running = true, lastError = null) }

        scope.launch {
            settings = SettingsStore.flow(this@LocationService).first()
            startLocationUpdates()
        }
        return START_STICKY
    }

    @SuppressLint("MissingPermission") // dozvolu provjerava ekran prije pokretanja servisa
    private fun startLocationUpdates() {
        val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, INTERVAL_MS)
            .setMinUpdateIntervalMillis(INTERVAL_MS)
            .build()
        fused.requestLocationUpdates(request, callback, Looper.getMainLooper())
    }

    private fun stopShift() {
        fused.removeLocationUpdates(callback)
        ShiftState.status.update { it.copy(running = false) }
        ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        fused.removeLocationUpdates(callback)
        scope.cancel()
        ShiftState.status.update { it.copy(running = false) }
        super.onDestroy()
    }

    private fun startInForeground() {
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(CHANNEL_ID, "Smjena", NotificationManager.IMPORTANCE_LOW)
        )

        val openApp = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Smjena je aktivna")
            .setContentText("Šaljem lokaciju busa")
            .setSmallIcon(android.R.drawable.ic_menu_mylocation)
            .setOngoing(true)
            .setContentIntent(openApp)
            .build()

        ServiceCompat.startForeground(
            this,
            NOTIFICATION_ID,
            notification,
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION else 0,
        )
    }

    companion object {
        private const val ACTION_STOP = "hr.krizevcibus.driver.STOP"
        private const val CHANNEL_ID = "shift"
        private const val NOTIFICATION_ID = 1
        private const val INTERVAL_MS = 4_000L

        fun start(context: Context) =
            ContextCompat.startForegroundService(context, Intent(context, LocationService::class.java))

        fun stop(context: Context) {
            context.startService(Intent(context, LocationService::class.java).setAction(ACTION_STOP))
        }
    }
}
