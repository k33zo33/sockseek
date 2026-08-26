# License and distribution - AGPL path A

## 3. Licencni i distribucijski model - Put A

Projekt nastavlja koristiti postojeći Sockseek kod pod GNU Affero General Public License v3. To je svjesna odluka: aplikacija ostaje otvorenog izvornog koda, a izmjene servera, enginea, API-ja i UI-ja objavljuju se pod istom licencom. Ovaj dokument nije pravni savjet, ali implementacija mora poštovati praktične zahtjeve licence i zadržati postojeći LICENSE.

### 3.1. Obavezne implementacijske mjere

- Ne uklanjati postojeći LICENSE niti copyright obavijesti.

- Dodati ekran Settings > About > License s nazivom licence, tekstom bez jamstva i poveznicom na izvorni kod.

- Distribucijski paket mora sadržavati THIRD-PARTY-NOTICES i uputu gdje se preuzima Corresponding Source.

- Svaki javno dostupan daemon build mora korisniku jasno ponuditi izvorni kod točno te verzije.

- Datoteke koje su značajno izmijenjene trebaju imati jasnu povijest kroz Git i release notes; ne umetati lažne autore.

- Frontend, backend i packaging skripte smatraju se dijelom istog proizvoda i ostaju AGPL-kompatibilni.

- Automatski generirani OpenAPI i migracije moraju biti u repozitoriju.

> **Release gate**  
> Nijedan javni binary release ne smije biti objavljen dok About ekran, LICENSE, THIRD-PARTY-NOTICES, source URL i release source tag nisu prisutni i testirani.
>
> Operativni popis za to nalazi se u `docs/release-checklist.md`.
