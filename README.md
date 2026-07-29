# Projects

```text
Projects/
├── ICP.sln                 # 含 ICP + TEL.IntegrationHub
├── ICP/                    # ICP ASP.NET Core MVC
└── TELIntegrationHub/      # TEL Integration Hub（單一 Web API）
```

```powershell
dotnet build ICP.sln

dotnet run --project ICP\ICP.csproj
dotnet run --project TELIntegrationHub\TEL.IntegrationHub.csproj
```
