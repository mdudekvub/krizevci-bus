package hr.krizevcibus.driver

import kotlinx.coroutines.flow.MutableStateFlow

enum class Direction(val api: String, val label: String) {
    ToKolodvor("ToKolodvor", "Prema Kolodvoru"),
    ToGroblje("ToGroblje", "Prema Groblju"),
}

data class ShiftStatus(
    val running: Boolean = false,
    val direction: Direction = Direction.ToKolodvor,
    val lastSentAt: Long? = null,
    val lastError: String? = null,
    val accuracyMeters: Float? = null,
)

// Zajedničko stanje između servisa (šalje lokaciju) i ekrana (prikazuje status)
object ShiftState {
    val status = MutableStateFlow(ShiftStatus())
}
