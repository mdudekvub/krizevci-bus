import { useEffect, useState } from "react";
import { API_URL, getLines } from "./api";
import type { Line } from "./types";
import { useVehicles } from "./useVehicles";
import BusMap from "./components/BusMap";
import Panel from "./components/Panel";

export default function App() {
  const [lines, setLines] = useState<Line[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [hidden, setHidden] = useState<Set<number>>(() => new Set());
  const { vehicles, connected } = useVehicles();

  useEffect(() => {
    getLines()
      .then(setLines)
      .catch(() => setError(`Linije se nisu učitale s ${API_URL}. Provjeri je li backend pokrenut.`));
  }, []);

  const toggleLine = (id: number) =>
    setHidden((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <div className="app">
      <BusMap lines={lines} hidden={hidden} vehicles={vehicles} />
      <Panel
        lines={lines}
        hidden={hidden}
        onToggle={toggleLine}
        vehicles={vehicles}
        connected={connected}
        error={error}
      />
    </div>
  );
}
