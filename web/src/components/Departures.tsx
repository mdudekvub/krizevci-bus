import { useEffect, useState } from "react";
import { getDepartures } from "../api";
import type { Departure, Direction, Line } from "../types";
import { useNow } from "../useNow";

const FROM: Record<Direction, string> = {
  ToKolodvor: "S Groblja",
  ToGroblje: "S Kolodvora",
};

export default function Departures({ line }: { line: Line }) {
  const [deps, setDeps] = useState<Departure[] | null>(null);
  const [failed, setFailed] = useState(false);
  const now = useNow(30_000);

  useEffect(() => {
    getDepartures(line.id).then(setDeps).catch(() => setFailed(true));
  }, [line.id]);

  if (failed) return <p className="muted">Vozni red se nije učitao.</p>;
  if (!deps) return <p className="muted">Učitavam vozni red…</p>;
  if (deps.length === 0) return <p className="muted">{line.name} danas ne vozi.</p>;

  const current = new Date(now).toTimeString().slice(0, 5); // "HH:mm"

  return (
    <div className="departures">
      {(Object.keys(FROM) as Direction[]).map((dir) => {
        const next = deps.filter((d) => d.direction === dir && d.time >= current).slice(0, 4);
        return (
          <div key={dir}>
            <h3>{FROM[dir]}</h3>
            {next.length ? (
              <ol>
                {next.map((d) => (
                  <li key={d.time}>{d.time}</li>
                ))}
              </ol>
            ) : (
              <p className="muted">Nema više polazaka danas.</p>
            )}
          </div>
        );
      })}
    </div>
  );
}
