# Vigilant

Vigilant je prototip AML aplikacije za pregled i istragu sumnjivih tokova novca kroz graf bazu.

Aplikacija prikazuje klijente, racune, transakcije, IP adrese i uredjaje kao mrezu cvorova. Ideja je da se lakse vide obrasci kao sto su kruzni tokovi, brzo prebacivanje novca, smurfing i slicne AML situacije.

## Tehnologije

Backend:

- .NET 8
- ASP.NET Core Web API
- Clean Architecture
- MediatR
- Neo4j.Driver
- SignalR
- Swagger

Frontend:

- React
- TypeScript
- Zustand
- Axios
- react-force-graph-2d
- obican CSS

Baza:

- Neo4j

## Pokretanje

Prvo pokreni Neo4j:

```powershell
docker compose up -d neo4j
```

Zatim pokreni backend:

```powershell
dotnet run --project .\backend\src\Vigilant.Api\Vigilant.Api.csproj --launch-profile http
```

Zatim frontend:

```powershell
cd .\frontend
npm install
npm run dev
```

## Korisne adrese

Frontend:

```text
http://localhost:5173
```

Swagger:

```text
http://localhost:5028/swagger
```

Neo4j Browser:

```text
http://localhost:7474
```

Neo4j login:

```text
Username: neo4j
Password: vigilant_dev_password
```

Bolt adresa za backend:

```text
bolt://localhost:7687
```

## Kako se koristi

Na frontend strani se nalazi levi panel sa alertima i desno graf mreza.

U grafu:

- plavi cvor je klijent
- zeleni cvor je racun
- zuti cvor je transakcija
- ljubicasti cvor je IP adresa
- roze cvor je uredjaj

Klikom na racun ili klijenta aplikacija fokusira povezane alerte u levom panelu. Hover preko cvora prikazuje osnovne informacije o njemu.

## Test podaci

Za ubacivanje demo podataka koristi se endpoint:

```text
POST /api/transactions/seed
```

Bazu mozes da ocistis u Neo4j Browser-u ovom komandom:

```cypher
MATCH (n) DETACH DELETE n;
```

## Provera build-a

Backend:

```powershell
dotnet build .\Vigilant.slnx
```

Frontend:

```powershell
cd .\frontend
npm run build
```

## Napomena

Ne treba commitovati `.env`, `node_modules`, `dist`, `bin` ili `obj` foldere.
