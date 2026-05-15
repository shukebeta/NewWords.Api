# Local Development Guide

This guide helps you set up a local development environment for NewWords.Api using Docker Compose.

## Prerequisites

- Docker Desktop or Docker Engine with Docker Compose
- .NET 8 SDK
- Git

## Quick Start

1. Start the infrastructure services:
```bash
docker compose up -d
```

2. Create your local configuration:
```bash
cp src/NewWords.Api/appsettings.Local.json.example src/NewWords.Api/appsettings.Local.json
```

3. Run the API:
```bash
dotnet run --project src/NewWords.Api --launch-profile Local
```

## Services

The docker-compose stack provides three services:

| Service | URL | Description |
|---------|-----|-------------|
| MySQL | `127.0.0.1:3306` | Database server |
| Redis | `127.0.0.1:6379` | Cache server |
| Seq | http://localhost:5341 | Log viewer UI |

### Accessing Services

**MySQL** (use the dev credentials):
```bash
mysql -h 127.0.0.1 -P 3306 -u newwords -pnewwordspw newwords
```

**Redis**:
```bash
redis-cli -h 127.0.0.1 -p 6379 ping
```

**Seq UI**: Open http://localhost:5341 in your browser.

## Configuration

The `appsettings.Local.json.example` file contains the connection strings for the docker services:

```json
{
  "DatabaseConnectionOptions": {
    "ConnectionString": "Server=127.0.0.1;Port=3306;Database=newwords;User Id=newwords;Password=newwordspw;Charset=utf8mb4;",
    "DbType": "MySql",
    "ApplicationName": "NewWords"
  },
  "Redis": {
    "ConnectionString": "127.0.0.1:6379",
    "Database": 0,
    "ProjectPrefix": "newwords.api"
  },
  "SeqServerUrl": "http://localhost:5341"
}
```

Note: `127.0.0.1` is used instead of `localhost` for MySQL to avoid connection protocol issues on some systems.

## Database Migrations

Database migration scripts are located in `migration_scripts/`. Apply them manually:

```bash
mysql -h 127.0.0.1 -P 3306 -u newwords -pnewwordspw newwords < migration_scripts/01_analyze_duplicates.sql
mysql -h 127.0.0.1 -P 3306 -u newwords -pnewwordspw newwords < migration_scripts/02_merge_duplicates.sql
# ... etc for each migration file
```

Or apply all migrations at once:
```bash
for f in migration_scripts/*.sql; do
  mysql -h 127.0.0.1 -P 3306 -u newwords -pnewwordspw newwords < "$f"
done
```

## Stopping Services

Stop all services:
```bash
docker compose down
```

Stop and remove volumes (clears all data):
```bash
docker compose down -v
```

## Troubleshooting

**Port already in use**: If port 3306, 6379, or 5341 is already in use, modify the port mappings in `docker-compose.yml`.

**Connection refused**: Ensure services are running with `docker compose ps`.

**Data persistence**: Data is stored in Docker volumes. Use `docker compose down -v` to reset.

## Security Note

The credentials in `.env.example` and `appsettings.Local.json.example` are for **development only**. Do not use these credentials in production.
