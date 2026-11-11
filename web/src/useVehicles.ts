import { useEffect, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { API_URL, getVehicles } from "./api";
import type { Vehicle } from "./types";

// Početno stanje povuče s API-ja, a zatim sluša SignalR za svaku novu poziciju
export function useVehicles() {
  const [byId, setById] = useState<Record<number, Vehicle>>({});
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const load = () =>
      getVehicles()
        .then((list) => {
          if (!cancelled) setById(Object.fromEntries(list.map((v) => [v.id, v])));
        })
        .catch(() => {});

    const conn = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/vehicles`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on("VehicleUpdated", (v: Vehicle) => setById((prev) => ({ ...prev, [v.id]: v })));
    conn.onreconnecting(() => setConnected(false));
    conn.onreconnected(() => {
      setConnected(true);
      load(); // nadoknadi propušteno dok je veza bila prekinuta
    });
    conn.onclose(() => setConnected(false));

    load();
    conn
      .start()
      .then(() => !cancelled && setConnected(true))
      .catch(() => {}); // u dev modu StrictMode prekine prvo spajanje, to je očekivano

    return () => {
      cancelled = true;
      conn.stop();
    };
  }, []);

  return { vehicles: Object.values(byId), connected };
}
