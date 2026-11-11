import { useEffect, useState } from "react";
import { getArrivals } from "../api";
import type { Arrival, Stop } from "../types";
import { directionLabel, lineNumber } from "../format";

const REFRESH_MS = 15_000;

// Sadržaj popupa stajališta. React-leaflet ga prikazuje tek kad se popup otvori,
// pa se dolasci dohvaćaju samo dok korisnik gleda stanicu.
export default function StopPopup({ stop }: { stop: Stop }) {
  const [arrivals, setArrivals] = useState<Arrival[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const load = () =>
      getArrivals(stop.code)
        .then((a) => {
          if (!cancelled) {
            setArrivals(a);
            setFailed(false);
          }
        })
        .catch(() => !cancelled && setFailed(true));

    load();
    const t = setInterval(load, REFRESH_MS);
    return () => {
      cancelled = true;
      clearInterval(t);
    };
  }, [stop.code]);

  return (
    <div className="stop-popup">
      <strong className="stop-name">{stop.name}</strong>
      {failed && <p className="muted">Dolasci se nisu učitali.</p>}
      {!failed && !arrivals && <p className="muted">Učitavam dolaske…</p>}
      {arrivals?.length === 0 && <p className="muted">Danas više nema dolazaka na ovu stanicu.</p>}
      {arrivals && arrivals.length > 0 && (
        <ul className="arrivals">
          {arrivals.map((a) => (
            <li key={`${a.lineId}-${a.direction}`}>
              <span className="swatch small" style={{ background: a.color }}>
                {lineNumber(a.lineName)}
              </span>
              <span className="arrival-dir">{directionLabel(a.direction)}</span>
              <span className={`arrival-time${a.live ? " live-time" : ""}`}>
                {a.minutes <= 0 ? "stiže" : `${a.minutes} min`}
              </span>
            </li>
          ))}
        </ul>
      )}
      {arrivals?.some((a) => !a.live) && <p className="note">Bez oznake uživo: procjena po voznom redu.</p>}
    </div>
  );
}
