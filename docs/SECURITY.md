# Security, privacy and operational rules

See also the release-focused [threat model](threat-model.md).

## Sprint 16 additions

- Packaged Soulseek account passwords are accepted only by the authenticated
  loopback account endpoint and persisted through `ISecretStore`. SQLite and API
  responses contain only the username, opaque secret reference and status metadata.
- Windows uses user-bound DPAPI for persistent secrets. Other supported desktop
  platforms use AES-GCM with an application-local random key; the key directory is
  mode `0700` and key/encrypted payload files are mode `0600` on Unix. Neither key
  material nor plaintext secret payloads enter logs, SQLite or diagnostics.
- Account changes are rejected while engine jobs are active or queued. An accepted
  change restarts only the idle engine, preventing an account switch from silently
  cancelling a transfer.
- The wishlist scheduler never writes query text or exception messages to logs.
- SQLite keeps only the locally entered query, bounded schedule/status, engine ids,
  result count and a stable error code. Raw Soulseek results are not duplicated in
  the wishlist table.
- Privilege notifications are bounded to 100 in-memory records and are not persisted.
  Notification usernames are absent from logs and SignalR invalidation payloads;
  only the authenticated HTTP snapshot exposes them for local acknowledgement UX.
- Chat state is bounded to 500 in-memory messages and 20 joined-room intents. Message
  text, usernames and private-message protocol ids are not persisted or logged.
- Chat SignalR invalidation contains only a revision and stable reason. Message text
  and usernames are returned only by the authenticated loopback HTTP snapshot.
- Soulseek share roots are separate explicit opt-ins and are never inferred from
  library or download roots. The immutable share snapshot includes only supported,
  completed audio files below readable enabled roots and skips reparse points.
- Remote share paths are normalized virtual paths and never interpreted as local
  paths. Opening a shared file revalidates canonical containment, every directory
  segment, reparse attributes and snapshot size before returning a read stream.
- Share-root CRUD and rebuild routes require the local session token. Local root
  paths are available only through that protected loopback snapshot; peer responses
  contain virtual paths only. A successful root mutation invalidates the previous
  snapshot before rebuild so cancellation fails closed, and removing configuration
  never removes physical audio.
- Upload visibility is available only through the authenticated loopback API. The
  daemon exposes a shared-file basename, never the transfer exception, remote IP or
  full local/remote path. It retains at most 100 terminal summaries in memory and
  sends only revision/reason invalidations over SignalR; progress invalidations are
  coalesced.

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
