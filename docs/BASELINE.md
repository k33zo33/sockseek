# Repository baseline and upstream synchronization

## 4. Polazno stanje postojećeg forka

Polazna točka je javni fork k33zo33/sockseek, grana master, commit ef36306c86046757d76d6c1158a48c7b2f58dc2c. U trenutku izrade dokumenta upstream fiso64/sockseek sadrži 25 dodatnih commitova. Fork nema vlastitu divergenciju koju treba čuvati, pa je preporučeno prvo napraviti kontrolirani fast-forward na provjereni upstream commit.

### 4.1. Dijelovi koje zadržavamo

| Projekt / komponenta | Vrijednost za novu aplikaciju | Strategija |
| --- | --- | --- |
| Sockseek.Core | DownloadEngine, job model, extractori, pretraga, rangiranje, downloader, skip logika, organizacija datoteka. | Zadržati i omotati adapterom; refaktorirati samo uz testove. |
| Sockseek.Api | Postojeći DTO-i i SockseekApiClient. | Proširiti verzioniranim app ugovorima; ne lomiti postojeći protokol. |
| Sockseek.Server | ASP.NET Core daemon, EngineSupervisor, state store, OpenAPI i SignalR. | Pretvoriti u lokalni application host; zadržati legacy endpointove. |
| Sockseek.Cli | Dokaz da lokalni i remote backend rade; napredni korisnici. | Održavati kompatibilnost i koristiti za dijagnostiku. |
| Test projekti | Velika postojeća pokrivenost core, CLI i server ponašanja. | Svi testovi moraju ostati zeleni nakon svakog sprinta. |
| Benchmark projekt | Mjerenje sortera, projekcija i velikih rezultata. | Proširiti za playlist resolution i library scan. |

### 4.2. Uočeni tehnički dug koji ne blokira prvi UI

- DownloadEngine je prevelik orkestrator i sam kod ga označava kao God Class. Ne prepisivati ga u početnim sprintovima; izolirati ga gatewayem.

- Job objekti su mutable i šalju PropertyChanged s background threadova. UI ne smije dobivati direktne Job instance; smije dobivati samo immutable snapshot DTO-e.

- M3uEditor upravlja i indeksom i playlistom. Razdvajanje je kasniji refactor, nakon funkcionalnog playera.

- Docker ostaje sekundarni headless packaging put. Sprint 0 je uskladio Dockerfile s `net10.0`, ali daemon/compose workflow i dalje treba zaseban pregled prije nego što se tretira kao polished deployment put.

- Daemon u baselineu nema dovoljno lokalne aplikacijske autentikacije. Desktop izdanje mora uvesti localhost session token i default bind samo na loopback.

### 4.3. Obavezni početni Git postupak

```bash
git checkout master
git pull --ff-only origin master
git tag sockseek-ui-baseline-ef36306 ef36306c86046757d76d6c1158a48c7b2f58dc2c
git remote add upstream https://github.com/fiso64/sockseek.git
git fetch upstream
git checkout -b codex/sprint-00-baseline-sync
git merge --ff-only upstream/master

dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

### 4.4. Sprint 0 development base

Sprint 0 currently treats commit `21066a6` (`docs: close out sprint 0 baseline validation`) on branch `codex/sprint-00-baseline-sync` as the validated development base built on top of synchronized upstream commit `7bcc1909e24083453716fda38a23e6663c0b78d6`.

This is the commit that should be referenced in the first Sprint 0 PR until a later reviewed commit intentionally supersedes it.

### 4.5. Baseline sync gate

Prije prvog funkcionalnog sprinta Codex mora zaključati reproducibilnu početnu točku. Ovaj gate sprječava da se UI i novi application sloj grade nad nepoznatom ili djelomično sinkroniziranom verzijom enginea.

- Working tree mora biti čist, bez lokalnih necommitiranih izmjena.

- Remote origin mora pokazivati na k33zo33/sockseek, a upstream na fiso64/sockseek.

- Tag sockseek-ui-baseline-ef36306 mora pokazivati na dokumentirani baseline commit ef36306c86046757d76d6c1158a48c7b2f58dc2c.

- Codex mora dohvatiti upstream i izraditi compare izvještaj prije bilo kakvog mergea.

- Fast-forward je dopušten samo ako nema vlastite divergencije i nakon pregleda breaking promjena.

- Nakon sinkronizacije obavezni su dotnet restore, Release build i cijeli test suite.

- Konačni commit koji postaje razvojna baza mora se zapisati u docs/baseline.md i u prvi sprint PR.

> **STOP UVJET:** ako fast-forward više nije moguć, ako testovi baselinea ne prolaze ili se pojavi licencna/kompatibilnosna nejasnoća, Codex mora stati, dokumentirati razliku i otvoriti ADR/PR za odluku. Ne smije koristiti force-push niti silom prepisati master.
