# Security, privacy and operational rules

## 15. Sigurnost, privatnost i operativna pravila

### 15.1. Lokalni API

- Bind default isključivo na loopback.

- Daemon pri startu generira 256-bitni session token i zapisuje ga u file s user-only permissions ili siguran IPC handshake.

- Desktop šalje token u Authorization headeru; Swagger/OpenAPI UI nije izložen u release buildu osim ako je developer mode uključen.

- Remote bind je post-MVP i zahtijeva poseban authentication ADR.

- CORS nije univerzalno otvoren; dopušten je samo desktop origin/loopback model koji je stvarno potreban.

### 15.2. File-system sigurnost

- Remote Soulseek filename nikad ne postaje putanja bez sanitizacije i canonical path provjere.

- Output path mora ostati unutar konfiguriranog root direktorija.

- Symlink i junction traversal mora biti testiran na podržanim OS-ovima.

- Brisanje datoteke koristi recycle/trash kada je moguće ili eksplicitnu potvrdu za permanent delete.

- Library scan preskače system directories i ne slijedi symlinkove po defaultu.

### 15.3. Privatnost i logovi

- Telemetry je isključena po defaultu.

- Logovi ne smiju sadržavati provider tokene, OAuth code, Soulseek lozinku, puni Authorization header ni privatne playlist URL parametre.

- Diagnostics export mora imati redaction korak i korisniku prikazati što se izvozi.

- External provider raw metadata zadržava se samo koliko je potrebno za sync i troubleshooting.

- Disconnect account akcija briše secret i omogućuje brisanje provider snapshot podataka bez brisanja lokalne glazbe.
