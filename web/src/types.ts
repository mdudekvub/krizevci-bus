export type Direction = "ToKolodvor" | "ToGroblje";
export type DayType = "Weekday" | "Weekend";

export interface Stop {
  id: number;
  code: string;
  name: string;
  lat: number;
  lng: number;
}

export interface LineDirection {
  direction: Direction;
  stops: Stop[];
  route: [number, number][];
}

export interface Line {
  id: number;
  name: string;
  color: string;
  directions: LineDirection[];
}

export interface Position {
  lat: number;
  lng: number;
  speed: number;
  heading: number;
  timestamp: string;
}

export interface Vehicle {
  id: number;
  name: string;
  lineId: number | null;
  direction: Direction | null;
  isActive: boolean;
  position: Position | null;
}

export interface Departure {
  direction: Direction;
  dayType: DayType;
  time: string;
}
export interface Arrival {
  lineId: number;
  lineName: string;
  color: string;
  direction: Direction;
  minutes: number;
  live: boolean;
  vehicleName: string | null;
}
