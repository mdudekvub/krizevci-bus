import type { Departure, Line, Vehicle } from "./types";

export const API_URL: string = import.meta.env.VITE_API_URL ?? "http://localhost:5050";

async function get<T>(path: string): Promise<T> {
  const res = await fetch(API_URL + path);
  if (!res.ok) throw new Error(`${path}: ${res.status}`);
  return res.json() as Promise<T>;
}

export const getLines = () => get<Line[]>("/api/lines");
export const getVehicles = () => get<Vehicle[]>("/api/vehicles");
export const getDepartures = (lineId: number) => get<Departure[]>(`/api/lines/${lineId}/departures`);
