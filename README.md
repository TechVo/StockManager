# Správa skladu

Webová aplikace v .NET 10 pro správu skladu produktů s jednou filtrovatelnou stránkou s gridem. Klient v Blazor WebAssembly čte a mění data přes REST API, data jsou v souboru SQLite a přistupuje se k nim přes EF Core. Zadání je v [docs/zadani.md](docs/zadani.md).

## Spuštění

Potřeba je jen .NET 10 SDK.

```bash
dotnet run --project src/StockManager.Server
```

Aplikace běží na `http://localhost:5080`. Při prvním spuštění vytvoří `src/StockManager.Server/stock.db` a naplní ji testovacími daty ze souboru [seed-data.json](src/StockManager.Server/Data/seed-data.json).

Existující databáze se znovu nenaplňuje, takže změny počtů kusů přežijí restart. Návrat k výchozím datům, například po úpravě JSON souboru:

```bash
dotnet run --project src/StockManager.Server -- --reset-db
```

Testy:

```bash
dotnet test
```

## Struktura

| Projekt | Obsah |
|---|---|
| `src/StockManager.Server` | REST API, EF Core a SQLite, naplnění databáze, hostování klienta |
| `src/StockManager.Web` | Blazor WebAssembly: stránka, filtr a grid |
| `src/StockManager.Contracts` | DTO sdílená serverem a klientem |
| `tests/StockManager.Api.Tests` | integrační testy API proti skutečnému SQLite souboru |

## REST API

| Metoda | Cesta | Popis |
|---|---|---|
| GET | `/api/clients`, `/api/categories`, `/api/products` | možnosti filtru |
| GET | `/api/stock?clientIds=1&categoryIds=2&productIds=3` | řádky gridu |
| PUT | `/api/stock/{clientId}/{productId}` | změna počtu kusů, tělo `{ "quantity": 5 }`, vrací 204, 400 nebo 404 |
| GET | `/api/clients/{code}`, `/api/categories/{code}`, `/api/products/{code}` | detail záznamu, vrací 200 nebo 404 |
| PUT | `/api/categories/{code}` | změna názvu, tělo `{ "name": "..." }`, vrací 204, 400, 404 nebo 409 |
| PUT | `/api/clients/{code}`, `/api/products/{code}` | změna názvu a kategorií, tělo `{ "name": "...", "categoryCodes": ["KAT_000001"] }`, vrací 204, 400, 404 nebo 409 |

## Rozhodnutí

- **Řádek gridu je dvojice klient a produkt.** Počet kusů patří k této dvojici, takže produkt ve více kategoriích má jeden řádek s kategoriemi vedle sebe a stejné množství se v gridu neopakuje.
- **Klient vidí produkt, pokud s ním sdílí aspoň jednu kategorii.** U řádku se ukazují jen tyto společné kategorie a jen podle nich funguje filtr kategorií.
- **Kategorie a produkty mají neměnný kód.** Kód vrací `/api/categories` i `/api/products`, takže přejmenování nic nerozbije. Sloupec `Code` je v každé tabulce unikátní a každý typ má vlastní předponu. Kód (`KAT_000001`, `PROD_000001`) generuje databáze z id, které se nikdy nepoužije znovu, takže ho nejde změnit ani zduplikovat. Testovací data dávají kód každé kategorii a produktu, ostatní záznamy se na ně odkazují tímto kódem a seeder z něj určí id, takže kód nezávisí na pořadí v souboru. Vazby v databázi a filtr gridu používají interní id.
- **Detail a hledání podle kódu.** Každý klient, kategorie a produkt má stránku `/detail/{kód}`. Hledání ve filtru pozná typ záznamu podle předpony (`KLI_`, `KAT_`, `PROD_`) a detail otevře. Malá písmena a mezery okolo kódu nevadí.
- **Změna kategorií mění sklad.** Nově viditelné dvojice klient a produkt dostanou řádek s 0 kusy a prázdné skryté řádky se smažou. Změna, která by skryla řádek s kusy, se odmítne s odpovědí 409, aby počty nezmizely bez upozornění.
- **Filtr:** hodnoty v jednom seznamu se kombinují přes OR, seznamy mezi sebou přes AND a prázdný seznam řádky neomezuje.
- **Blazor WebAssembly místo Blazor Server,** protože stránka má data získávat přes REST API.
- **Řazení probíhá v prohlížeči,** protože SQLite neřadí české znaky správně.
- **`EnsureCreated` místo migrací,** protože databáze je testovací a jde kdykoli znovu vytvořit.

## Vědomá zjednodušení

Aplikace nemá stránkování, přihlašování ani přidávání a mazání klientů, produktů a kategorií. Při souběžné úpravě stejného řádku platí poslední zápis.
