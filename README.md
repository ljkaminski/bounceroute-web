# BounceRoute

Scheduling and route management for a party rental company that delivers bounce houses, water slides, and other inflatables.

The office books rentals, builds each day's route, and assigns trucks and crews. Crews see the stops in order, check items off as they load them, and mark jobs complete. Crew pay is based on how many units (bounce houses, slides) each person set up or picked up, so the app counts those automatically.

![Today's route](docs/screenshots/route.png)

| Job details | Crew pay |
| --- | --- |
| ![Job details](docs/screenshots/job.png) | ![Crew pay](docs/screenshots/crew.png) |

## Stack

| Layer | Tech |
| --- | --- |
| Frontend | React 19, TypeScript, Vite, Tailwind CSS v4, TanStack Query, React Router |
| API | ASP.NET Core 10 minimal APIs, EF Core 10 |
| Database | PostgreSQL 16 |
| Contract | OpenAPI document generated at build time → TypeScript types via `openapi-typescript` → typed client with `openapi-fetch` |
| Tests | xUnit + `WebApplicationFactory` + Testcontainers (real Postgres) · Vitest + Testing Library |
| CI | GitHub Actions: build, test, and fail if the OpenAPI document or generated types are stale |

```
React (web/) ──/api──▶ ASP.NET Core API (api/BounceRoute.Api) ──EF Core──▶ PostgreSQL
      ▲                          │
      └── src/api/schema.d.ts ◀──┴── openapi/BounceRoute.Api.json (written on every API build)
```

Changing a request or response type in C# changes the OpenAPI file, which changes the generated TypeScript types, so the frontend stops compiling until it's updated. CI checks that both generated files are committed.

## How the data fits together

- A **booking** is one customer rental: address, ground surface, site notes, and the **items** going out.
- Every booking has exactly two **jobs**: a setup and a pickup. Each job has its own time, truck, and crew (via **job assignments**).
- An item is either a **unit** (counts toward pay) or an **extra** like a generator (doesn't).
- Pay totals = units on *completed* jobs a crew member was assigned to. The "weekend" runs Friday through Monday, since Monday pickups belong to the weekend.
- Job times are stored in UTC. A day's route is computed in the business time zone (`Business:TimeZone`, default `America/New_York`), so an 11 PM job stays on the right day and days with DST changes are handled. Tests cover both.

## Run it locally

Prerequisites: .NET 10 SDK, Node 24, and Postgres (either your own or Docker).

```bash
# 1. Database: either use Docker…
docker compose up -d
# …or your own Postgres:
#   CREATE ROLE bounceroute LOGIN PASSWORD 'bounceroute' CREATEDB;
#   CREATE DATABASE bounceroute OWNER bounceroute;

# 2. API on http://localhost:5125 (applies migrations and seeds made-up sample data in Development)
dotnet run --project api/BounceRoute.Api

# 3. Web on http://localhost:5173 (proxies /api to the API)
cd web
npm install
npm run dev
```

OpenAPI JSON is served at http://localhost:5125/openapi/v1.json.

## Tests

```bash
dotnet test        # API integration tests; needs Docker running (Testcontainers starts Postgres)
cd web && npm test # unit and component tests
```

## Common tasks

```bash
# After changing the EF model
dotnet ef migrations add <Name> --project api/BounceRoute.Api --output-dir Data/Migrations

# After changing API contracts: build the API, then regenerate the frontend types
dotnet build api/BounceRoute.Api
cd web && npm run gen:api
```

## Project layout

```
api/BounceRoute.Api/
  Domain/        entities (Booking, Job, BookingItem, CrewMember, JobAssignment)
  Contracts/     request/response records; these become the OpenAPI schema
  Endpoints/     minimal API route groups: route & jobs, bookings, crew & pay
  Data/          DbContext, migrations, business clock, dev seed data
api/BounceRoute.Api.Tests/   integration tests against real Postgres
web/src/
  api/           generated schema, typed client, React Query hooks
  pages/         Route, Job, Bookings, Booking form, Crew & pay
  components/    layout, job card, badges
openapi/         generated OpenAPI document (committed)
```

## Roadmap

- [ ] Sign-in with roles: owner, office, crew (crew see only their jobs)
- [ ] Deploy (API + Postgres + static site) with a public demo
- [ ] Connect the .NET MAUI crew app to this API, keeping SQLite as an offline cache
- [ ] Text crews when their schedule changes
- [ ] Customer availability calendar so units can't be double-booked

All names, addresses, and phone numbers in the seed data and screenshots are made up.
