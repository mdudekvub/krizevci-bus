// Izveze trasu svake linije i smjera iz seed.json u GPX s vremenima,
// za lažnu vožnju u Android emulatoru (Extended controls -> Location -> Routes -> Import GPX).
//
//   node tools/export-gpx.mjs
//   -> gpx/linija-1-ToKolodvor.gpx, gpx/linija-1-ToGroblje.gpx, ...

import fs from "node:fs";

const seedPath = process.argv[2] ?? "backend/Data/seed.json";
const outDir = "gpx";

// Isto kao simulator i ETA: 18 min po smjeru, 20 s stajanja na stanici
const TRIP_SECONDS = 18 * 60;
const DWELL_SECONDS = 20;

const seed = JSON.parse(fs.readFileSync(seedPath, "utf8"));
const stops = Object.fromEntries(seed.stops.map((s) => [s.code, s]));

const toRad = (d) => (d * Math.PI) / 180;
function distance([lat1, lng1], [lat2, lng2]) {
  const R = 6371000;
  const dLat = toRad(lat2 - lat1);
  const dLng = toRad(lng2 - lng1);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(h));
}

function nearestIndex(points, [lat, lng], from) {
  let best = from;
  let bestD = Infinity;
  for (let i = from; i < points.length; i++) {
    const d = distance(points[i], [lat, lng]);
    if (d < bestD) { bestD = d; best = i; }
  }
  return best;
}

fs.mkdirSync(outDir, { recursive: true });

for (const line of seed.lines) {
  for (const dir of line.directions) {
    // Trasa po ulicama ako postoji, inače ravno od stanice do stanice
    const points = dir.route?.length >= 2 ? dir.route : dir.stops.map((c) => [stops[c].lat, stops[c].lng]);

    const cum = [0];
    for (let i = 1; i < points.length; i++) cum.push(cum[i - 1] + distance(points[i - 1], points[i]));
    const total = cum.at(-1);

    // Indeksi točaka trase na kojima su međustanice
    const stopIdx = new Set();
    let from = 0;
    for (const code of dir.stops.slice(1, -1)) {
      from = nearestIndex(points, [stops[code].lat, stops[code].lng], from);
      stopIdx.add(from);
    }

    const speed = total / (TRIP_SECONDS - DWELL_SECONDS * stopIdx.size); // m/s
    const start = Date.UTC(2026, 0, 1, 8, 0, 0);
    let t = 0;
    const trkpts = [];
    const pt = (p, sec) =>
      `      <trkpt lat="${p[0]}" lon="${p[1]}"><time>${new Date(start + sec * 1000).toISOString()}</time></trkpt>`;

    for (let i = 0; i < points.length; i++) {
      if (i > 0) t += (cum[i] - cum[i - 1]) / speed;
      trkpts.push(pt(points[i], t));
      if (stopIdx.has(i)) {
        t += DWELL_SECONDS;
        trkpts.push(pt(points[i], t)); // ista točka nakon 20 s = bus stoji na stanici
      }
    }

    const name = `${line.name} ${dir.direction}`;
    const gpx = `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="krizevci-bus" xmlns="http://www.topografix.com/GPX/1/1">
  <trk>
    <name>${name}</name>
    <trkseg>
${trkpts.join("\n")}
    </trkseg>
  </trk>
</gpx>
`;
    const file = `${outDir}/${line.name.toLowerCase().replace(/\s+/g, "-")}-${dir.direction}.gpx`;
    fs.writeFileSync(file, gpx);
    console.log(`${file}: ${(total / 1000).toFixed(2)} km, ${Math.round(t / 60)} min`);
  }
}
