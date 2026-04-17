# Vigilant AML

Vigilant is a graph-native Anti-Money Laundering (AML) prototype built with .NET 8, Clean Architecture, MediatR CQRS, Neo4j, SignalR, and a React + TypeScript analyst dashboard.

The project models financial activity as a graph so analysts can investigate clients, accounts, transactions, IP addresses, devices, circular money flows, structuring, fan-out behavior, shared infrastructure, and PEP/offshore risk patterns.

## Tech Stack

Backend:

```text
.NET 8 ASP.NET Core Web API
Clean Architecture
MediatR CQRS
Neo4j.Driver
SignalR
Swagger / OpenAPI
Bogus for demo data generation
```

Frontend:

```text
React + TypeScript
Zustand
Axios
SignalR client
react-force-graph-2d
Vanilla CSS with dark glassmorphism styling
```

Database:

```text
Neo4j 5 Community
```

## Local URLs

```text
Frontend:       http://localhost:5173
Swagger UI:     http://localhost:5028/swagger
Neo4j Browser:  http://localhost:7474
Neo4j Bolt:     bolt://localhost:7687
SignalR Hub:    http://localhost:5028/hubs/alerts
```

Default local Neo4j credentials from `docker-compose.yml`:

```text
Username: neo4j
Password: vigilant_dev_password
```

Do not commit local `.env` files. They are ignored by `.gitignore`.

## Project Layout

```text
D:\Vigilant
|-- Vigilant.slnx
|-- docker-compose.yml
|-- backend
|   `-- src
|       |-- Vigilant.Domain
|       |   |-- Alerts
|       |   `-- Graph
|       |-- Vigilant.Application
|       |   |-- Alerts
|       |   |-- Clients
|       |   |-- Common
|       |   |-- Graph
|       |   `-- Transactions
|       |-- Vigilant.Infrastructure
|       |   |-- Graph
|       |   |-- Options
|       |   `-- Seed
|       `-- Vigilant.Api
|           |-- Contracts
|           |-- Controllers
|           |-- Hubs
|           `-- Services
`-- frontend
    |-- src
    |   |-- api
    |   |-- components
    |   |-- realtime
    |   |-- store
    |   `-- styles
    `-- package.json
```

## Neo4j Graph Model

Nodes:

```text
Client      (Id, Name, RiskScore, IsPep)
Account     (Id, IBAN, Balance, CountryCode)
Transaction (Id, Amount, Timestamp, Currency)
IpAddress   (Address, CountryCode)
Device      (DeviceId, BrowserFingerprint)
```

Relationships:

```text
(Client)-[:OWNS]->(Account)
(Account)-[:SENT]->(Transaction)
(Transaction)-[:RECEIVED_BY]->(Account)
(Transaction)-[:EXECUTED_FROM_IP]->(IpAddress)
(Transaction)-[:EXECUTED_ON_DEVICE]->(Device)
```

Some manual/demo data may also contain:

```text
(Client)-[:USES_DEVICE]->(Device)
```

The backend detection rules primarily use the transaction execution path through `EXECUTED_ON_DEVICE` and `EXECUTED_FROM_IP`.

## AML Detection Rules

The backend currently evaluates these rules through `INeo4jRepository`, `Neo4jRepository`, and the AML detection service pipeline.

```text
Circular Flow
Detects account transfer cycles where money returns to the originating account.

Smurfing / Structuring
Detects more than 5 outgoing transactions from the same account in 24 hours where each transaction is below 10,000 and the total exceeds 40,000.
Severity: Medium for 40,000-80,000, High above 80,000.

Rapid Fan-Out
Detects one account sending money to more than 4 distinct destination accounts within 1 hour.
Severity: High.

Shared Device or IP
Detects 2 or more clients sharing the same device fingerprint or IP address when at least one client has RiskScore above 60.
Severity: High for 2 clients, Critical for 3 or more.

Boomerang / Round-trip
Detects money leaving an account and returning to it within 7 days through a path of 2 to 6 intermediate transfers.
Severity: Medium for path length <= 4, High for longer paths.

PEP + Offshore
Detects PEP clients sending transactions above 50,000 from accounts located in offshore jurisdictions.
Hardcoded offshore country codes: VG, KY, PA, SC, BZ, VU.
Severity: Critical.
```

Alerts are broadcast to the frontend through SignalR and are also available via REST.

## Risk Score

`RiskScoreService` recomputes `Client.RiskScore` after transactions are processed. The score is capped at 100.

```text
+15 if any smurfing alert exists for the client in the last 7 days
+20 if any rapid fan-out alert exists in the last 7 days
+25 if shared device/IP alert exists
+20 if round-trip alert exists
+30 if PEP + offshore alert exists
```

Risk endpoint:

```http
GET /api/clients/{id}/risk
```

Example response shape:

```json
{
  "clientId": "c7",
  "riskScore": 80,
  "contributingAlerts": [
    {
      "alertType": "PepOffshore",
      "description": "PEP + offshore alert exists for this client in the last 7 days.",
      "weight": 30
    }
  ]
}
```

## Backend Endpoints

```http
GET  /api/alerts
GET  /api/graph/overview?nodeLimit=250
GET  /api/graph/accounts/{iban}?depth=6
GET  /api/clients/{id}/risk
POST /api/transactions/process
POST /api/transactions/seed
```

Notes:

```text
/api/graph/overview returns a bounded full-network graph for the dashboard.
/api/graph/accounts/{iban} returns the local network around one account.
/api/transactions/seed still exists for API testing, but the frontend no longer uses it from the main toolbar.
```

## Frontend Dashboard

The React UI is an analyst dashboard with two panels.

Left panel:

```text
Vigilant AML branding
Real-time alert feed
All / Critical / High / Medium severity filters
Unread alert count
Investigate button per alert
SignalR-driven alert updates
Loading skeleton cards
```

Right panel:

```text
Interactive react-force-graph-2d network
Prikazi celu mrezu button for the default full graph overview
Manual IBAN search input
Fit View button
Live SignalR connection status
Node hover tooltips
Alert investigation highlighting
Dark glassmorphism visual style
```

Node colors:

```text
Client:      #3b82f6
Account:     #10b981
Transaction: #f59e0b
IpAddress:   #8b5cf6
Device:      #ec4899
```

Client nodes are sized by `RiskScore` from a minimum radius of 6px to a maximum radius of 20px.

## Run Locally

Start Neo4j:

```powershell
docker compose up -d neo4j
```

Run the backend API:

```powershell
dotnet run --project .\backend\src\Vigilant.Api\Vigilant.Api.csproj --launch-profile http
```

Run the frontend:

```powershell
cd .\frontend
npm.cmd install
npm.cmd run dev
```

Open:

```text
http://localhost:5173
```

Optional frontend `.env`:

```text
VITE_API_BASE_URL=http://localhost:5028
```

## Seed And Reset Data

The API seeder can generate a larger randomized graph for stress testing:

```http
POST /api/transactions/seed
```

Example body:

```json
{
  "clientCount": 20,
  "accountCount": 34,
  "randomTransactionCount": 72,
  "circularFlowCount": 3
}
```

For a clean Neo4j database, run this in Neo4j Browser:

```cypher
MATCH (n) DETACH DELETE n;
```

The current frontend workflow does not need random seed data. Use `Prikazi celu mrezu` for the default graph overview or search a specific IBAN manually.

Useful test IBANs from the simple demo dataset used during development:

```text
RS35105008123456789
RS35105008777888999
RS35105008987654321
RS35105008999000111
```

## Build And Verify

Backend:

```powershell
dotnet build D:\Vigilant\Vigilant.slnx
```

Frontend:

```powershell
cd D:\Vigilant\frontend
npm.cmd run build
```

Expected result:

```text
Backend build succeeds with 0 errors.
Frontend TypeScript and Vite build succeeds.
```

## Development Notes

```text
Commit source only. Do not commit .env, node_modules, dist, bin, obj, or .verify-build.
Restart the backend after adding API routes, controllers, or DI registrations.
Refresh the frontend after backend contract changes.
Use Neo4j Browser for direct Cypher inspection and Swagger for REST endpoint testing.
```