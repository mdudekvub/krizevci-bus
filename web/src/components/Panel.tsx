import { useState } from "react";
import type { Line, Vehicle } from "../types";
import { STALE_MS, directionLabel, lineNumber } from "../format";
import { useNow } from "../useNow";
import Departures from "./Departures";

interface Props {
  lines: Line[];
  hidden: Set<number>;
  onToggle: (id: number) => void;
  vehicles: Vehicle[];
  connected: boolean;
  error: string | null;
}

export default function Panel({ lines, hidden, onToggle, vehicles, connected, error }: Props) {
  const [open, setOpen] = useState<number | null>(null);
  const now = useNow(5000);
  const running = vehicles.filter((v) => v.isActive && v.lineId !== null);

  return (
    <aside className="panel">
      <header>
        <h1>Križevci Bus</h1>
        <span className={`live${connected ? " on" : ""}`}>{connected ? "Uživo" : "Spajanje…"}</span>
      </header>

      {error && <p className="error">{error}</p>}

      <section>
        {lines.map((l) => (
          <div key={l.id} className="line">
            <div className="line-row">
              <label className="line-toggle">
                <input type="checkbox" checked={!hidden.has(l.id)} onChange={() => onToggle(l.id)} />
                <span className="swatch" style={{ background: l.color }}>
                  {lineNumber(l.name)}
                </span>
                {l.name}
              </label>
              <button
                type="button"
                className="link"
                aria-expanded={open === l.id}
                onClick={() => setOpen(open === l.id ? null : l.id)}
              >
                {open === l.id ? "Sakrij" : "Polasci"}
              </button>
            </div>
            {open === l.id && <Departures line={l} />}
          </div>
        ))}
      </section>

      {running.length > 0 && (
        <section className="buses">
          <h2>Na cesti</h2>
          <ul>
            {running.map((v) => {
              const line = lines.find((l) => l.id === v.lineId);
              const p = v.position;
              const stale = !p || now - Date.parse(p.timestamp) > STALE_MS;
              const status = stale ? "nema signala" : p.speed > 1 ? `${Math.round(p.speed)} km/h` : "stoji";
              return (
                <li key={v.id}>
                  <span className="swatch small" style={{ background: line?.color }}>
                    {line ? lineNumber(line.name) : "?"}
                  </span>
                  <span>
                    <strong>{v.name}</strong>
                    <span className="muted">
                      {v.direction ? ` ${directionLabel(v.direction)}` : ""}, {status}
                    </span>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      )}
    </aside>
  );
}
