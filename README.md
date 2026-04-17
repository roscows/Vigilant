# Vigilant AML

Vigilant is a graph-native AML starter built with .NET 8, Clean Architecture, MediatR CQRS, Neo4j, React, Zustand, Axios, and SignalR.

## Clean Architecture Layout

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
|       |   |-- Common\Graph
|       |   |-- Graph
|       |   `-- Transactions
|       |-- Vigilant.Infrastructure
|       |   |-- Graph
|       |   `-- Options
|       `-- Vigilant.Api
|           |-- Contracts
|           |-- Controllers
|           `-- Hubs
`-- frontend
    |-- src
    |   |-- api
    |   |-- hooks
    |   |-- realtime
    |   |-- store
    |   `-- styles
    `-- package.json
```

## Neo4j Graph Model

Nodes:

```text
Client      (Id, Name, RiskScore)
Account     (Id, IBAN, Balance)
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

The transaction payload requires sender IBAN, receiver IBAN, amount, currency, device ID, and IP address. Optional client snapshots are supported so the repository can create `Client-OWNS-Account` relationships when upstream KYC/account context is available.

## Backend

Key entry points:

```text
backend/src/Vigilant.Application/Transactions/ProcessTransaction/TransactionProcessorCommand.cs
backend/src/Vigilant.Application/Transactions/ProcessTransaction/TransactionProcessorCommandHandler.cs
backend/src/Vigilant.Application/Common/Graph/INeo4jRepository.cs
backend/src/Vigilant.Infrastructure/Graph/Neo4jRepository.cs
backend/src/Vigilant.Api/Controllers/TransactionsController.cs
backend/src/Vigilant.Api/Controllers/AlertsController.cs
backend/src/Vigilant.Api/Controllers/GraphController.cs
backend/src/Vigilant.Api/Hubs/AlertsHub.cs
```

Run Neo4j:

```powershell
docker compose up -d neo4j
```

Run API:

```powershell
dotnet run --project .\backend\src\Vigilant.Api\Vigilant.Api.csproj
```

Swagger UI is available in Development at:

```text
https://localhost:<api-port>/swagger
```

## Circular Flow Rule

`Neo4jRepository.BuildCircularFlowDetectionQuery(maxTransfers)` builds the Cypher query that detects account cycles shaped as repeated transfer blocks:

```text
(Account)-[:SENT]->(Transaction)-[:RECEIVED_BY]->(Account)
```

The query searches for a path that returns to the same origin account within the configured transfer count, filters to recent transactions, totals the flow amount, and emits Medium/High/Critical alert DTOs.

## Frontend

Key entry points:

```text
frontend/src/api/amlApi.ts
frontend/src/hooks/useGraphData.ts
frontend/src/realtime/alertsHub.ts
frontend/src/store/alertsStore.ts
frontend/src/styles/globals.css
```

Install and run:

```powershell
cd .\frontend
npm.cmd install
npm.cmd run dev
```

Build:

```powershell
npm.cmd run build
```

Optional frontend environment file:

```text
VITE_API_BASE_URL=http://localhost:5028
```

## Verification Completed

```powershell
dotnet build D:\Vigilant\Vigilant.slnx
npm.cmd run build
```

Both backend and frontend builds pass.

## Demo Seeder and Visualization

Seed a demo graph from Swagger or the frontend button:

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

The seeder uses Bogus to create realistic clients, accounts, transactions, IP addresses, and devices. It deliberately creates A -> B -> C -> A circular-flow cycles inside the last 24 hours, runs the existing circular-flow rule, and broadcasts detected alerts through `/hubs/alerts`.

The React dashboard now uses `react-force-graph-2d` through `frontend/src/components/AmlGraphViewer.tsx`. It supports zooming, panning, fit-to-view, AML color conventions, and hover detail cards for IBANs, transaction amounts, IP addresses, devices, and client risk scores.


