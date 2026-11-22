# ServiceDesk

ServiceDesk is an IT ticket management application. Its ASP.NET Core API stores
tickets, categories, and accounts in SQL Server through Entity Framework Core.

## Run the API locally

Use .NET 10 and SQL Server LocalDB. From the repository root, restore the
dependencies and apply the migrations. Set a signing key once per development
computer. Keep an existing key if you want existing access tokens to stay valid.

```powershell
dotnet tool restore
dotnet restore
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
@{ 'Jwt:SigningKey' = $key } | ConvertTo-Json -Compress | dotnet user-secrets set --project src/ServiceDesk.Api
Remove-Variable key
dotnet ef database update --project src/ServiceDesk.Api -- --environment Development
dotnet run --project src/ServiceDesk.Api
```

The local API listens on `http://localhost:5018`. Use
`POST /api/auth/register` to create a standard user and `POST /api/auth/login`
to get a 30-minute JWT. Send it in the `Authorization: Bearer <token>` header.
Standard users create and read their own tickets. Admins see all tickets and
can edit, categorize, and delete them. Tickets created before sign-in was
added remain visible to admins.

## Run the Angular frontend

Use Node.js 24 with Angular 21. Keep the API running on port 5018, then open
a second terminal from the repository root:

```powershell
npm ci --prefix src/ServiceDesk.Web
npm run start --prefix src/ServiceDesk.Web
```

Open [ServiceDesk in the browser](http://localhost:4200). Create an employee
account or sign in. You can submit a ticket and use the queue filters. Admins
can select **Edit** on a ticket to change its details, category, and status.
Select **Activity** to see the ticket's status history, post comments, and
attach or download a PNG, JPEG, or PDF file up to 5 MB. The
admin **Dashboard** shows status and category counts. Selecting a status card
opens the matching queue. On the new-ticket form, selecting **Network & VPN**
reveals an optional connection type that is saved with the description. The
frontend keeps the access token in memory, so reloading the page requires
signing in again. The Angular dev server forwards `/api` requests to the
ASP.NET Core API through `proxy.conf.json`.

## Provision a local admin

After applying the migrations, run the following in PowerShell with a new
email address. Choose a password with at least 12 characters, including
uppercase and lowercase letters, a number, and a symbol.

```powershell
$env:Admin__Email = Read-Host 'Admin email'
$env:Admin__Password = Read-Host 'Admin password' -MaskInput
try {
    dotnet run --project src/ServiceDesk.Api -- --seed-admin=true
} finally {
    Remove-Item Env:Admin__Email, Env:Admin__Password
}
```

The command creates the admin and exits. Start the API separately with
`dotnet run --project src/ServiceDesk.Api`.

## Send requests from Visual Studio

Open `Auth.http` and select the `local` environment. Create
`src/ServiceDesk.Api/http-client.env.json.user` to set local variables.
Use a disposable test account and keep the `.user` file out of Git.

```json
{
  "local": {
    "accountEmail": "replace-with-your-test-email@example.test",
    "accountPassword": "replace-with-your-test-password",
    "accessToken": "replace-with-the-login-response-accessToken"
  }
}
```

The last request in `Auth.http` uses the named login response directly.
For the other `.http` files, copy the login response's `accessToken` into
the local environment file. Use an admin token for ticket changes and deletion.
