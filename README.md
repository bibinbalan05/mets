# Mets.Replenishment

Repository structure

- `Mets.Replenishment.Api` — ASP.NET Core Web API project that exposes the backend endpoints.
- `Mets.Replenishment.UI` — Blazor WebAssembly (WASM) frontend.
- `Mets.Replenishment.Core` — Core domain models and interfaces.
- `Mets.Replenishment.Infrastructure` — Data access, repository implementations and integrations.
- `Mets.Replenishment.Tests` — Unit and integration tests.

Prerequisites

- .NET8 SDK 

Getting started

1. Clone the repository

 git clone <repo-url>

2. Restore dependencies

 dotnet restore

3. Build the solution

 dotnet build

Running the applications

- API
 - Change directory to `Mets.Replenishment.Api` and run:

 dotnet run

 - The API will start and listen on the configured URLs (see `appsettings.json` or environment variables).

 dotnet run

 - Open the URL reported in the console (typically `http://localhost:5005` or another port) in your browser. 

Tests

- Run all tests from the solution root:

 dotnet test
