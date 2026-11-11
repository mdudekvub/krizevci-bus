import { useMemo } from "react";
import { CircleMarker, MapContainer, Marker, Polyline, Popup, TileLayer, Tooltip, ZoomControl } from "react-leaflet";
import L from "leaflet";
import type { Line, Stop, Vehicle } from "../types";
import { STALE_MS, directionLabel, lineNumber } from "../format";
import { useNow } from "../useNow";

const CENTER: [number, number] = [46.0235, 16.5425];
// Karta se ne može pomaknuti izvan Križevaca ni odzumirati dalje od grada
const BOUNDS: [[number, number], [number, number]] = [
  [45.998, 16.515],
  [46.048, 16.575],
];

interface Props {
  lines: Line[];
  hidden: Set<number>;
  vehicles: Vehicle[];
}

export default function BusMap({ lines, hidden, vehicles }: Props) {
  const now = useNow(5000);
  const visible = useMemo(() => lines.filter((l) => !hidden.has(l.id)), [lines, hidden]);

  // Svako fizičko stajalište jednom, s popisom linija koje tamo staju
  const stops = useMemo(() => {
    const map = new Map<string, { stop: Stop; serves: string[] }>();
    for (const l of visible)
      for (const d of l.directions)
        for (const s of d.stops) {
          const entry = map.get(s.code) ?? { stop: s, serves: [] };
          entry.serves.push(`${l.name} ${directionLabel(d.direction)}`);
          map.set(s.code, entry);
        }
    return [...map.values()];
  }, [visible]);

  return (
    <MapContainer
      center={CENTER}
      zoom={15}
      minZoom={14}
      maxZoom={19}
      maxBounds={BOUNDS}
      maxBoundsViscosity={1}
      className="map"
      zoomControl={false}
    >
      <TileLayer
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        maxZoom={19}
      />
      <ZoomControl position="bottomright" />

      {visible.flatMap((l) =>
        l.directions.map((d) => (
          <Polyline
            key={`${l.id}-${d.direction}`}
            positions={d.route}
            pathOptions={{
              color: l.color,
              weight: 5,
              opacity: 0.8,
              lineCap: "round",
              // prema Kolodvoru puna linija, prema Groblju točkasta
              dashArray: d.direction === "ToGroblje" ? "1 10" : undefined,
            }}
          />
        )),
      )}

      {stops.map(({ stop, serves }) => (
        <CircleMarker
          key={stop.code}
          center={[stop.lat, stop.lng]}
          radius={6}
          pathOptions={{ color: "#1d2a1f", weight: 2, fillColor: "#ffffff", fillOpacity: 1 }}
        >
          <Popup>
            <strong>{stop.name}</strong>
            <ul className="serves">
              {serves.map((s) => (
                <li key={s}>{s}</li>
              ))}
            </ul>
          </Popup>
        </CircleMarker>
      ))}

      {vehicles.map((v) => {
        const line = lines.find((l) => l.id === v.lineId);
        if (!line || !v.position || hidden.has(line.id)) return null;
        const stale = now - Date.parse(v.position.timestamp) > STALE_MS;
        return <BusMarker key={v.id} vehicle={v} line={line} stale={stale} />;
      })}
    </MapContainer>
  );
}

function BusMarker({ vehicle, line, stale }: { vehicle: Vehicle; line: Line; stale: boolean }) {
  const p = vehicle.position!;
  const moving = p.speed > 1;
  // Ikona se mijenja samo kad se smjer promijeni za 15°, inače bi se
  // svake sekunde stvarala nova i animacija kretanja ne bi radila
  const heading = Math.round(p.heading / 15) * 15;

  const icon = useMemo(
    () =>
      L.divIcon({
        className: "bus-marker",
        iconSize: [36, 36],
        iconAnchor: [18, 18],
        html: `<div class="bus${stale ? " stale" : ""}" style="--c:${line.color}">
          ${moving && !stale ? `<div class="bus-arrow" style="transform:rotate(${heading}deg)"></div>` : ""}
          <span>${lineNumber(line.name)}</span>
        </div>`,
      }),
    [line.color, line.name, heading, moving, stale],
  );

  return (
    <Marker position={[p.lat, p.lng]} icon={icon}>
      <Tooltip direction="top" offset={[0, -20]}>
        {vehicle.name}, {line.name}
        {vehicle.direction ? ` ${directionLabel(vehicle.direction)}` : ""}
        {stale ? " (nema signala)" : ""}
      </Tooltip>
    </Marker>
  );
}
