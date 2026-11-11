# Deploy

Cijeli sustav (Postgres, API, simulator, web s Caddyjem) radi u Docker Composeu na jednom Linux serveru. Caddy servira web, prosljeđuje `/api` i `/hubs` na backend i sam izvadi HTTPS certifikat.

## 0. Lokalni test produkcije (na Macu)

Prije servera provjeri da se sve builda i radi u kontejnerima.

U `.env` dodaj:

```
DOMAIN=localhost
BUS_API_KEY=neki_lokalni_kljuc
```

Ugasi lokalni backend i simulator ako rade, pa:

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f
```

Otvori https://localhost. Browser će upozoriti na certifikat jer je lokalni, to je u redu. Gašenje:

```bash
docker compose -f docker-compose.prod.yml down
```

## 1. Server na Azureu

Azure portal → **Virtual machines** → **Create**:

- **Image:** Ubuntu Server 24.04 LTS
- **Size:** najmanji B-serije s barem 1 GB RAM-a (procijeni cijenu u Azure kalkulatoru da ti kredit potraje)
- **Authentication:** SSH public key (Azure ga može generirati, spremi `.pem` file)
- **Inbound ports:** 22 (SSH), 80 (HTTP), 443 (HTTPS)

Nakon kreiranja zapiši **Public IP address**. Kod Networking postavki dodaj i pravilo za **UDP 443** (HTTP/3, nije obavezno).

## 2. Domena

U Namecheapu (ili Name.com) → domena → **Advanced DNS** → dodaj zapis:

| Type | Host | Value |
|---|---|---|
| A Record | @ | IP servera |

Provjera (može potrajati do sat vremena):

```bash
dig +short krizevcibus.me
```

Mora vratiti IP servera **prije** pokretanja, inače Caddy ne može izvaditi certifikat.

## 3. Priprema servera

```bash
chmod 600 ~/Downloads/kljuc.pem
ssh -i ~/Downloads/kljuc.pem azureuser@IP_SERVERA
```

Na serveru:

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
exit
```

Ponovno se spoji (da vrijedi docker grupa), pa:

```bash
git clone https://github.com/mdudekvub/krizevci-bus.git
cd krizevci-bus
cp .env.example .env
nano .env
```

U `.env` postavi pravu domenu, jaku lozinku baze i **novi** ključ:

```bash
openssl rand -hex 32   # za BUS_API_KEY
openssl rand -hex 16   # za DB_PASSWORD
```

## 4. Pokretanje

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
```

Prvi build traje nekoliko minuta. Kad su sve usluge `running`, otvori https://tvoja-domena.

Logovi:

```bash
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f web
```

## Ažuriranje nakon novih promjena

```bash
cd krizevci-bus
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

Baza ostaje sačuvana u Docker volumenu. Migracije se primjenjuju automatski kod pokretanja API-ja.

## Česti problemi

- **Caddy ne dobije certifikat:** domena još ne pokazuje na IP (`dig`) ili portovi 80/443 nisu otvoreni u Azure Networkingu.
- **Busevi se ne miču:** `logs simulator`. Ako piše 401, `BUS_API_KEY` u `.env` je prazan ili kriv.
- **API vraća 503 za pozicije:** `BUS_API_KEY` nije postavljen.
- **Promjena `seed.json`:** seed se izvršava samo na praznoj bazi. Za ponovno punjenje: `docker compose -f docker-compose.prod.yml down -v` (briše bazu!) pa `up -d --build`.
