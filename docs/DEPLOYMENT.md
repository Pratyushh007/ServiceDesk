# Deploy ServiceDesk

The Docker image serves Angular and ASP.NET Core on port 8080. The frontend calls
the API on the same origin. SQL Server runs separately.

## Build the release image

From the repository root:

```powershell
docker build -t servicedesk:local .
```

## Prepare the destination

Use a fresh SQL Server database for a demo deployment. The runtime connects using
`ConnectionStrings__ServiceDesk`. Supply the connection string and a random
Base64 JWT signing key of at least 32 bytes as deployment secrets under
`ConnectionStrings__ServiceDesk` and `Jwt__SigningKey`. Keep the signing key
stable across replicas and releases.

Apply the existing EF Core migrations to the target database before starting the
app. To generate an idempotent SQL script for review:

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project src/ServiceDesk.Api --output servicedesk-migrations.sql -- --environment Development
```

Apply the script with an identity that can change the schema and with
`QUOTED_IDENTIFIER` enabled for the filtered Identity indexes. For example, use
`sqlcmd -S <server> -d <database> -b -I -i servicedesk-migrations.sql` with the
appropriate authentication options for the destination.

The application
identity should have read and write permissions on the application tables.
Configure HTTPS ingress with target port 8080. Set `/health` as the database
connectivity probe. It returns HTTP 200 when SQL Server is reachable and HTTP 503
otherwise.

To provision an admin, run the image once with the target database and JWT
settings, `Admin__Email` and `Admin__Password` supplied as secrets, and the
argument `--seed-admin=true`. Start the regular application without that flag.

## Verify the deployed release

1. Open the HTTPS URL and load the Angular sign-in page.
2. Check that `/health` reports HTTP 200 and an anonymous request to
   `/api/tickets` reports HTTP 401.
3. Sign in with a disposable employee account, submit a ticket, then check it
   from the admin queue.
