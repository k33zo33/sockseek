# Configuration and profiles

## 16. Konfiguracija i profili

Postojeći Sockseek config/profiles sustav ostaje izvor engine i download kvalitete. Nova baza čuva UI, library, account i player postavke. U prvoj fazi ne treba migrirati cijeli legacy config u SQLite.

| Vrsta postavke | Source of truth |
| --- | --- |
| Soulseek engine credentials i concurrency | Existing Sockseek config, kasnije ISecretStore + DB UI editor. |
| Download/search quality profiles | Existing profile catalog; UI ih dohvaća kroz API. |
| Download output paths | Existing DownloadSettings uz UI editor i validaciju. |
| Library roots | SQLite. |
| Provider accounts i sync | SQLite + ISecretStore. |
| Player preferences | SQLite. |
| UI theme/layout | Desktop local settings ili SQLite per profile. |
