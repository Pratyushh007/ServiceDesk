# Deploy the ServiceDesk PostgreSQL demo

The Docker image serves Angular and ASP.NET Core on port 8080. The frontend calls
the API on the same origin. PostgreSQL runs separately. Deploy the
`demo/postgresql` branch to use this setup.

## Build the release image

From the repository root:

```powershell
docker build -t servicedesk:local .
```

## Prepare the destination

Create a Supabase project on the Free plan and select **Connect → Session
pooler** for the IPv4 connection details. Use those values in an Npgsql
connection string:

```text
Host=<pooler-host>;Port=5432;Database=<database>;Username=<pooler-user>;Password=<password>;SSL Mode=Require
```

Use the exact values from the dashboard and quote the password in the connection
string if it contains a semicolon. Configure the connection string and a random
Base64 JWT signing key of at least 32 bytes as secrets named
`ConnectionStrings__ServiceDesk` and `Jwt__SigningKey`. Keep the signing key
stable across releases. ServiceDesk uses the `servicedesk` schema. Keep it out of
the list of schemas exposed by Supabase's Data API.

Apply the PostgreSQL migration before starting the app. With the same variables
set in the local PowerShell session, run:

```powershell
dotnet tool restore
dotnet ef database update --project src/ServiceDesk.Api -- --environment Development
```

To provision a demo admin, set `Admin__Email` and `Admin__Password` locally and
run `dotnet run --project src/ServiceDesk.Api -- --seed-admin=true` against the
demo database. Clear the two variables after provisioning. The application only
needs a database identity with read and write permissions on the application
tables at runtime.

In Render, create a Free Docker Web Service from the GitHub repository and select
`demo/postgresql`. Leave the root directory blank, set `PORT=8080`, and use
`/health` as the health check path. Enter the connection string and JWT key in
the service's environment settings.

## Verify the deployed release

1. Open the HTTPS URL and load the Angular sign-in page.
2. Check that `/health` reports HTTP 200 and an anonymous request to
   `/api/tickets` reports HTTP 401.
3. Sign in with a disposable employee account, submit a ticket, then check it
   from the admin queue.
