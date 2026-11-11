import type { Direction } from "./types";

export const STALE_MS = 60_000; // bez nove pozicije dulje od minute = nema signala

export const directionLabel = (d: Direction) =>
  d === "ToKolodvor" ? "prema Kolodvoru" : "prema Groblju";

// "Linija 1" -> "1"
export const lineNumber = (name: string) => name.replace(/\D/g, "") || name;
