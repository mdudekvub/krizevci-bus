// Izračuna trasu po stvarnim ulicama za svaku liniju i smjer (OSRM, OpenStreetMap)
// i upiše je u seed.json kao "route". Pokreće se ručno, samo kad se promijene stajališta.
//
//   node tools/generate-routes.mjs
//   node tools/generate-routes.mjs backend/Data/seed.json

import fs from "node:fs";

const path = process.argv[2] ?? "backend/Data/seed.json";
const OSRM = "https://router.project-osrm.org/route/v1/driving";

const seed = JSON.parse(fs.readFileSync(path, "utf8"));
const stops = Object.fromEntries(seed.stops.map((s) => [s.code, s]));

const round = (n) => Math.round(n * 1e6) / 1e6;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

for (const line of seed.lines) {
  for (const dir of line.directions) {
    const missing = dir.stops.filter((c) => !stops[c] || (stops[c].lat === 0 && stops[c].lng === 0));
    if (missing.length) {
      console.log(`${line.name} ${dir.direction}: preskačem, nema koordinata za ${missing.join(", ")}`);
      continue;
    }

    // OSRM traži "lng,lat" redom kojim bus vozi
    const coords = dir.stops.map((c) => `${stops[c].lng},${stops[c].lat}`).join(";");
    const url = `${OSRM}/${coords}?overview=full&geometries=geojson&continue_straight=true`;

    const res = await fetch(url);
    const data = await res.json();
    if (data.code !== "Ok") {
      console.log(`${line.name} ${dir.direction}: OSRM greška ${data.code} ${data.message ?? ""}`);
      continue;
    }

    const route = data.routes[0];
    dir.route = route.geometry.coordinates.map(([lng, lat]) => [round(lat), round(lng)]);
    console.log(`${line.name} ${dir.direction}: ${dir.route.length} točaka, ${(route.distance / 1000).toFixed(2)} km`);

    await sleep(1000); // javni OSRM server, ne smijemo ga zatrpati zahtjevima
  }
}

// Ispis: jednostavni nizovi i objekti idu u jedan red, da file ostane čitljiv
const text = JSON.stringify(seed, null, 2)
  .replace(/\[([^\[\]{}]*)\]/g, (_, inner) => `[${inner.trim().split(/,\s*/).join(", ")}]`)
  .replace(/\{([^\[\]{}]*)\}/g, (_, inner) => `{ ${inner.trim().split(/,\s*/).join(", ")} }`);
fs.writeFileSync(path, text + "\n");
console.log(`\nSpremljeno u ${path}`);
