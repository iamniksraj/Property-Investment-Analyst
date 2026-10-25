# Property Investment Analyst

Angular 19 frontend for analysing Bengaluru residential property investments, with an ASP.NET Core 10 Web API and SQL Server persistence for saved comparison workspaces.

The TypeScript calculation engine remains the source of truth for yield, EMI, snapshot cash flow, scenario cash flows, net sale proceeds, net profit and equity IRR. The API stores **inputs and scenario assumptions only**; the browser recalculates derived metrics after load.

This API has **no authentication**. Do not expose it on a public network in this phase.

## Prerequisites

- Node.js 18+ and npm
- .NET SDK 10.0 (10.0.402 used in development)
- SQL Server 2022 (Developer/Express/LocalDB) with permission to create databases, or a login that can use existing databases `PropertyInvestmentAnalyst` and `PropertyInvestmentAnalyst_Tests`
- `dotnet-ef` is pinned in `dotnet-tools.json` (restore with `dotnet tool restore`)

Windows Authentication against `localhost` is the development default (`Trusted_Connection=True`). SQL logins are supported by changing the connection string; never commit real passwords.

## Database

Schema (EF Core migration `InitialCreate`):

- `ComparisonAnalyses` — id, name, optional description, created/updated UTC, `SchemaVersion` (currently 1)
- `ComparedProperties` — financial inputs, `SortOrder`, FK to analysis with **cascade delete**
- `PropertyScenarioAssumptions` — conservative / base / optimistic rates per property, unique `(ComparedPropertyId, ScenarioName)`, check constraint on scenario name, cascade delete
- `MarketObservations` — sourced, dated asking prices / advertised rents / verified transactions, distinct from saved comparison assumptions

Money uses `decimal(18,2)`; rates and percentages use `decimal(9,4)`. Derived metrics are not stored.

Create or update the development database (does **not** drop an existing database):

```bash
dotnet tool restore
dotnet ef database update --project backend/PropertyInvestmentAnalyst.Api --startup-project backend/PropertyInvestmentAnalyst.Api
```

Configuration:

- Development: `backend/PropertyInvestmentAnalyst.Api/appsettings.json` (Windows auth, database `PropertyInvestmentAnalyst`)
- Template: `backend/PropertyInvestmentAnalyst.Api/appsettings.Development.json.example`
- Production: set `ConnectionStrings__DefaultConnection` and `Cors__AllowedOrigins__0` via environment variables. `appsettings.Production.json` contains placeholders only.

Integration tests use `PropertyInvestmentAnalyst_Tests` and **delete that test database** between tests. They do not drop `PropertyInvestmentAnalyst`.

## API contract

Base URL in development: `http://localhost:5231`

| Method | Path | Result |
| --- | --- | --- |
| GET | `/api/health` | `{ status, timestampUtc, environment }` |
| GET | `/api/comparisons` | List summaries |
| GET | `/api/comparisons/{id}` | Analysis + properties + three scenario blocks |
| POST | `/api/comparisons` | Create (201) |
| PUT | `/api/comparisons/{id}` | Replace properties/assumptions in one transaction |
| DELETE | `/api/comparisons/{id}` | 204 |
| GET | `/api/market-observations/template` | CSV header plus one labelled example row |
| GET | `/api/market-observations?locality=` | Search (optional `propertyType`, `configuration`, `recordType`) |
| GET | `/api/market-observations/stats?locality=` | Per-record-type aggregates |
| GET | `/api/investment-analyst/status` | Whether the OpenAI key is configured (never returns the key) |
| POST | `/api/investment-analyst/ask` | Explain supplied analysis context (read-only) |
| GET | `/api/market-observations/{id}` | One observation and provenance |
| POST | `/api/market-observations` | Manual entry |
| POST | `/api/market-observations/import/preview` | Validate CSV, no writes |
| POST | `/api/market-observations/import/commit` | Insert valid non-duplicates in one transaction |

Validation failures return ASP.NET `ValidationProblem` (`errors` dictionary). Missing ids are 404. Stale `expectedUpdatedUtc` is 409. CORS allows `http://localhost:4200` by default.

Example create:

```http
POST http://localhost:5231/api/comparisons
Content-Type: application/json

{
  "name": "Whitefield vs HSR",
  "description": "Illustrative",
  "properties": [
    {
      "name": "Whitefield 2 BHK",
      "locality": "Whitefield",
      "builderProject": "Illustrative project",
      "propertyType": "Apartment",
      "purchasePrice": 12000000,
      "superBuiltUpAreaSqFt": 1200,
      "monthlyRent": 40000,
      "monthlyMaintenance": 5000,
      "downPaymentPercent": 20,
      "annualInterestRatePercent": 8.5,
      "loanTenureYears": 20,
      "acquisitionCostPercent": 7,
      "holdingPeriodYears": 10,
      "vacancyPercent": 5,
      "annualPropertyTax": 0,
      "annualRepairAndMaintenance": 0,
      "conservative": { "propertyAppreciationPercent": 2, "rentGrowthPercent": 2, "vacancyPercent": 8, "operatingExpenseGrowthPercent": 6, "sellingCostPercent": 2 },
      "base": { "propertyAppreciationPercent": 5, "rentGrowthPercent": 4, "vacancyPercent": 5, "operatingExpenseGrowthPercent": 5, "sellingCostPercent": 2 },
      "optimistic": { "propertyAppreciationPercent": 8, "rentGrowthPercent": 6, "vacancyPercent": 3, "operatingExpenseGrowthPercent": 4, "sellingCostPercent": 2 }
    }
  ]
}
```

A `.http` file lives at `backend/PropertyInvestmentAnalyst.Api/PropertyInvestmentAnalyst.Api.http`.

## Run

Terminal 1 — API:

```bash
dotnet run --project backend/PropertyInvestmentAnalyst.Api --launch-profile http
```

Terminal 2 — Angular (`apiBaseUrl` is `http://localhost:5231` in `src/environments/environment.ts`):

```bash
npm start
```

Open `http://localhost:4200/compare`, `http://localhost:4200/market` or `http://localhost:4200/analyst`.

Production frontend build:

```bash
npx ng build
```

Override the production API origin in `src/environments/environment.prod.ts` (no secrets belong there).

## Tests

```bash
dotnet test backend/PropertyInvestmentAnalyst.slnx
npx ng test --no-watch --browsers=ChromeHeadless
```

Backend tests require local SQL Server. They were written against SQL Server, not EF Core InMemory. Docker/Testcontainers was not used because Docker is not required when a local instance is available.

## Market data methodology

This application does **not** have live Bengaluru market coverage. The file `data/bengaluru-market-observations.synthetic.csv` is an explicitly labelled **synthetic test fixture**. Source name is `Synthetic fixture — not a live listing`. Do not present those amounts as asking prices you could transact today.

### Record types

| Type | Meaning |
| --- | --- |
| `asking_price` | Advertised sale price. Not a completed transaction. |
| `advertised_rent` | Advertised monthly rent. Not a signed lease. |
| `verified_transaction` | A completed sale that a reviewer has marked as verified. None are included in the synthetic file. |
| `other` | Anything that does not fit the three categories above. |

Aggregates are computed **separately** for asking price, advertised rent and verified transactions. They are never blended into one “market average”.

### Area and price per square foot

Accepted area types: `super_built_up`, `built_up`, `carpet`, `plot`, `unspecified`. Units: `sq_ft`, `sq_m`.

Price per sq. ft. (sale) and monthly rent per sq. ft. are stored only when **all** of these hold:

- `areaValue` > 0
- `areaType` = `super_built_up`
- `areaUnit` = `sq_ft`

No conversion is applied between carpet, built-up, super built-up or square metres. If a statistics bucket mixes area definitions, amount medians may still be shown and per-sq-ft medians are omitted.

### Duplicates

A SHA-256 key is built from normalised source name, source record id, locality, project, configuration, record type, amount and observation date. Matching rows are skipped. Existing rows are never updated in place, including verified records and newer observations. A later observation date for the same source record is stored as a **new** row.

### Outliers, missing values, freshness

- Outliers: 1.5×IQR on amount within locality + configuration + record type + area type + unit, only when at least five comparable amounts exist. Flagged rows remain listed. They are excluded from aggregates unless `includeOutliers=true`.
- Missing optional fields stay null. Missing required fields fail the row. Price per sq. ft. is not calculated when area, area type or unit is missing or is not super built-up square feet.
- Stale: `observedOn` older than **180 days**. That cut-off is a product setting (`MarketObservation.StaleAfterDays`), not a Bengaluru market standard. Unverified: `verificationStatus = unverified`. `source_cited` only means a source name and reference were stored; it does not prove the row is a genuine listing.
- Published median (labelled as asking/rent/transaction median, not a valuation): at least **5** comparable non-outlier records. **3–4** records may show an indicative median with a sparse warning. Below 3, no median is published. 25th and 75th percentiles use the same n≥5 rule as the IQR helper.
- Evidence-quality labels (sample size, recency, verification, area comparability, source completeness, synthetic, outliers) describe the evidence, not the probability that a property is fairly priced.

### Synthetic versus genuine statistics

Search defaults to **including** labelled synthetic rows so the fixture remains usable. Statistics and assumption comparisons **never mix** genuine and synthetic amounts in one median. If any genuine row matches the filters, only genuine rows are aggregated. If the matching set is synthetic-only, aggregates are returned only when `includeSynthetic=true`, and they are labelled as synthetic test data — not a market conclusion.

### Historical trends

Monthly counts are returned for the selected cohort. A month needs at least **3** comparable observations before a median is shown. At least **3** such months are required before the series is labelled sufficient. Empty months are gaps; values are not interpolated. Synthetic dates may be plotted as a fixture timeline and are never described as real Bengaluru history. The current CSV fixture is not sufficient for historical trends.

### Assumption comparison

`POST /api/market-observations/assumption-comparison` compares a user’s purchase price and optional monthly rent with compatible asking-price and advertised-rent observations. Difference percent is `(assumption − median) / median`. The API does not change saved comparisons or financing inputs. A gap versus the median asking price is not proof that a property is undervalued or overpriced. Incompatible area types require `acknowledgeIncompatibleArea`; per-sq-ft figures stay omitted in that case.

### Import workflow

1. GET `/api/market-observations/template` or use `data/bengaluru-market-observations.synthetic.csv`.
2. POST the file to `/import/preview`.
3. POST the same file to `/import/commit` after reviewing valid / invalid / duplicate rows.
4. Failed commits roll back; preview never writes.

Angular `/market` can filter comparable observations, show locality summaries and evidence-quality notes, inspect provenance, compare a workspace property with observations, import CSV, and **explicitly** add a new comparison property from a selected observation. Existing comparison inputs are not replaced.

## Authorised market-data sources (Phase 8)

No genuine live Bengaluru listing or transaction feed is connected. The ingestion foundation is in place; access and licence approval are still required before any real-market import.

| Source | Evidence reviewed | Usable now? | Notes |
| --- | --- | --- | --- |
| 99acres / Info Edge | [Terms](https://www.99acres.com/info/terms-and-conditions): listings are proprietary; personal/internal use only; redistribution and derivative works are restricted. No public developer API is documented. Third-party “API” products are not Info Edge licences. | No | Contact Info Edge for a written B2B/data licence before any adapter is enabled. |
| Housing.com | Careers [terms of use](https://careers.housing.com/terms-of-use/) govern the consumer site. Help-centre “developer” pages are listing packages, not a data API. Unofficial wrappers exist; they are not Housing.com. | No | Do not scrape. Obtain written permission. |
| Kaveri (Karnataka IGR) | Official portal [kaveri.karnataka.gov.in](https://kaveri.karnataka.gov.in/) (the public fetch of the homepage returned HTTP 500 from this environment). Independent technical notes describe OTP + CAPTCHA access, not a documented commercial API. | No | Bypassing OTP/CAPTCHA is out of scope. |
| K-RERA | [rera.karnataka.gov.in](https://rera.karnataka.gov.in/home?language=en) publishes project registration HTML. No listing prices, advertised rents or a documented API were verified. | No | Useful for project identity later, not for asking-price statistics. |
| NHB RESIDEX | Official dashboards at [residex.nhbonline.org.in](https://residex.nhbonline.org.in/) include Bengaluru city indices, not apartment-level observations for Whitefield/HSR/etc. No public API documentation was found. | No | City index ≠ comparable 2 BHK observations. |
| data.gov.in / Karnataka OGD | Open-data portals exist; no verified apartment asking-price or IGR transaction API for Bengaluru localities was confirmed. NGDRS does not list Karnataka as a live IGR API state. | No | Re-check if a department publishes an explicit dataset + licence. |

**Recommended next step:** negotiate a licensed feed (or an official extract) that grants storage and commercial-analysis rights, then set `MarketData__LicensedFeed__BaseUrl` and `MarketData__LicensedFeed__ApiKey` on the server only. Until then, `GET /api/market-providers` reports `hasGenuineLiveFeed: false`.

### Provider-neutral ingestion

- `IMarketDataProvider` + `LicensedFeedMarketDataProvider` map a documented JSON envelope `{ "observations": [ … ] }` into `SaveMarketObservationRequest`.
- `MarketIngestionService` reuses validation, SHA-256 duplicate keys, outlier flags and SQL transactions. Policy is **insert-only**: duplicates are skipped; older or lower-quality rows for the same source record are skipped; **updated is always 0**. Existing verified or newer rows are never overwritten.
- Provider rows are capped at `source_cited`. A feed cannot mark `reviewed` and cannot turn an asking price into a verified transaction.
- `MarketImportRuns` stores refresh summaries (no secrets). Manual refresh: `POST /api/market-providers/licensed_feed/refresh` only when `MarketData:AllowManualRefresh` is true (Development default). In Production the flag is forced off even if configuration or environment variables set it to true. The API is unauthenticated; do not expose it publicly.
- Existing CSV preview/commit still works and records a `csv_file` run on commit.

Apply the non-destructive migration `MarketImportFoundation` (`ProviderKey` on `MarketObservations`, new `MarketImportRuns` table). Do not drop the development database.

## Manual persistence check

1. Start SQL Server, apply migrations, start the API and `npm start`.
2. Open `/compare`, load illustrative samples (or add properties), set an analysis name, click **Save comparison**.
3. Stop both processes and start them again.
4. Open `/compare`; the saved name should appear under **Saved comparisons**. Click **Open**. Inputs and independent Conservative/Base/Optimistic assumptions should match; table metrics should reappear from the TypeScript engine.
5. Change rent or a scenario rate, click **Update saved comparison**, then **Delete** to remove it.

Unsaved edits prompt before loading samples, clearing, or opening another analysis.

## Notes

- Maximum three properties per comparison.
- Client-generated local ids (`property-1`) are not sent as database keys; the API assigns Guids on save.
- Direct `System.Security.Cryptography.Pkcs` 10.0.13 is referenced to override the transitive 9.0.11 package pulled by EF Core SQL Server. Re-check `dotnet list package --vulnerable` after restore. The API remains unauthenticated.
- Dashboard component CSS still exceeds the 4 kB style budget warning from earlier phases.

## AI Investment Analyst (Phase 9)

The `/analyst` workspace sends the user question plus bounded context to `POST /api/investment-analyst/ask`. The TypeScript engine remains the source of truth: Angular supplies calculated yield, cash flow, net profit and IRR. C# does not recalculate them. The model has no SQL tools and cannot create, update or delete comparisons or observations.

**Provider:** OpenAI Chat Completions (`gpt-4o-mini` by default) via a server-side `IInvestmentAnalystProvider`. Credentials never belong in Angular `environment.ts` files.

Local setup (after the API already runs):

```bash
dotnet user-secrets set "InvestmentAnalyst:OpenAI:ApiKey" "YOUR_KEY" --project backend/PropertyInvestmentAnalyst.Api
```

Or set `InvestmentAnalyst__OpenAI__ApiKey`. Optional: `InvestmentAnalyst__OpenAI__BaseUrl` (default `https://api.openai.com/v1`) and `InvestmentAnalyst__OpenAI__Model`. Restart the API. With no key, status and ask return `AI not configured` plus setup guidance; Dashboard, Compare and Market still work.

Context retrieval:

- Optional `comparisonId` loads saved inputs/assumptions from SQL.
- Optional `observationIds` load those rows (max 8).
- Optional matching locality observations (max 8) when `includeMarketEvidence` is true.
- Browser-calculated metrics are validated for shape and range, then labelled as contextual TypeScript results.

Guardrails: synthetic observations stay labelled and are never genuine market evidence; asking prices are not transactions; missing numbers are not invented; answers are not financial advice. Session Q&A stays in browser memory and is not written to a new table.

The API remains unauthenticated. Do not expose it publicly. After authentication exists, bind comparison and observation context to the caller. Live OpenAI behaviour is not covered by automated tests (those use a fake provider and an `HttpMessageHandler`). No genuine live Bengaluru market feed is connected.
