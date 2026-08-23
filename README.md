# eShop Reference Application - "AdventureWorks"

A reference .NET application implementing an e-commerce website using a services-based architecture with [Aspire](https://aspire.dev/).

![eShop Reference Application architecture diagram](img/eshop_architecture.png)

![eShop homepage screenshot](img/eshop_homepage.png)

## Enterprise Banking & PCI-DSS Refactoring Overview

This fork of eShop has been refactored to align with enterprise banking standards
(PCI-DSS, ISO 20022, PSD2, AML). The payment path — `Ordering.API`,
`Ordering.Infrastructure`, `Payment.Shared`, and `PaymentProcessor` — now implements
the following controls:

### Financial Security & Data Protection (PCI-DSS)

- **Field-level AES-256-GCM encryption at rest**: `Aes256SensitiveDataProtector`
  (`src/Payment.Shared/Security/`) encrypts the payment PAN and security code before
  they reach the database via an EF Core value converter applied in
  `PaymentMethodEntityTypeConfiguration`. Keys come from configuration
  (`PaymentSecurity:EncryptionKey`, base64 256-bit) and never from source control;
  without a key a no-op protector is used (dev/test only). Authenticated decryption
  failures surface as a uniform `CryptographicException` to avoid oracle behavior.
- **PAN masking (PCI-DSS 3.3)**: `PanMasker` masks card numbers in every log and API
  payload (`XXXXXXXXXXXX1111` — last four digits only). HTTP request logging adds the
  masked PAN to the diagnostic scope, never the raw value.
- **Immutable financial audit trail (AML / PSD2)**: every financial command
  (order placement, payment authorization/settlement, cancellation) appends a
  `FinancialAuditEvent` to `ordering.financial_audit_events`. Entries carry event
  type, UTC timestamp, correlation id, actor, subject, amount/currency, outcome, the
  ISO 20022 message id, and a **SHA-256 hash chain** (`Hash` links to `PreviousHash`)
  making the ledger tamper-evident. The audit row is written inside the same ACID
  transaction as the state change (`EfFinancialAuditStore`).

### ISO 20022 & Banking Payment Standards

- `eShop.Payment.Shared.Iso20022` (`src/Payment.Shared/Iso20022/`) models the
  **pain.001.001.09** (CustomerCreditTransferInitiation) and **pacs.008.001.08**
  (FIToFICustomerCreditTransfer) message structures: end-to-end id
  (`ORDER-{n}`, 35 chars max), instructed amount with currency, debtor/creditor
  parties, and charge bearer — serialized with the real ISO 20022 XML element names.
- `PaymentProcessor` builds a pain.001 message for each authorization; the
  settlement path emits a pacs.008 message. Message ids are stored on the audit
  entries for cross-system traceability.
- **Idempotency**: all financial endpoints require an `x-requestid` header. The
  `IdempotencyEndpointFilter` (`Ordering.API/Infrastructure/Idempotency/`) reserves
  the token in Redis (`IIdempotencyStore`, atomic set-if-absent with a 24h TTL) to
  prevent duplicate execution, replays in-flight/duplicate submissions with HTTP 200
  and `X-Idempotent-Replay: true`, and the MediatR `IdentifiedCommand` pipeline keeps
  the per-command `RequestManager` dedup as a second layer (double-spend prevention).

### Database & Transaction Integrity

- `TransactionBehavior` wraps every command in an explicit transaction created via
  the **Npgsql EF Core execution strategy** (retry-on-transient-failure safe) using
  **serializable isolation for monetary commands** and read-committed otherwise.
- Polly resilience pipelines (`DatabaseResiliencePipelines`) retry monetary balance
  reads (`MonetaryReads`) and transient failures with exponential backoff; all
  `OrderQueries` reads route through them.
- EF Core migration `FinancialAuditAndPciEncryption` creates the audit table and
  widens the encrypted columns.

### DevOps & CI/CD

- Service images run as the **non-root `app` user (UID 1654)** via `ContainerUser`
  in `Directory.Build.props`; Redis (idempotency store) is wired to `ordering-api`
  in the AppHost.
- `.github/workflows/banking-ci.yml` builds the solution, runs the payment-flow
  unit tests (`Ordering.UnitTests`, `Application.UnitTests`), runs the Postgres-backed
  functional tests, and executes security linters (NuGet vulnerability scan, DevSkim,
  CodeQL).

### Configuration

| Setting | Purpose |
| ------- | ------- |
| `PaymentSecurity:EncryptionKey` | Base64-encoded 256-bit AES key for PAN encryption (optional; dev no-op protector without it) |
| `ConnectionStrings:redis` | Redis instance backing the idempotency token store |

## Getting Started

This version of eShop is based on .NET 10.

Previous eShop versions:
* [.NET 8](https://github.com/dotnet/eShop/tree/release/8.0)

### Prerequisites

- Clone the eShop repository: https://github.com/dotnet/eshop
- Install and start a supported OCI-compatible container runtime, such as [Docker Desktop](https://docs.docker.com/engine/install/) or [Podman](https://podman.io/)

#### Windows with Visual Studio
- Install [Visual Studio 2022 version 17.10 or newer](https://visualstudio.microsoft.com/vs/).
    - Select the following workloads:
        - `ASP.NET and web development` workload.
        - `Aspire SDK` component in `Individual components`.
        - Optional: `.NET Multi-platform App UI development` to run client apps

Or

- Run the following commands in an elevated PowerShell terminal to automatically configure your environment with the required tools to build and run this application. (A restart is required and included in the script below.)

```powershell
install-Module -Name Microsoft.WinGet.Configuration -AllowPrerelease -AcceptLicense -Force
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
Get-WinGetConfiguration -File .\.config\configuration.vs.winget | Invoke-WinGetConfiguration -AcceptConfigurationAgreements
```

Or

- From Dev Home go to `Machine Configuration -> Clone repositories`. Enter the URL for this repository. In the confirmation screen look for the section `Configuration File Detected` and click `Run File`.

#### Mac, Linux, & Windows without Visual Studio
- Install the latest [.NET 10 SDK](https://dot.net/download?cid=eshop)

Or

- On Windows, run the following commands in an elevated PowerShell terminal to automatically configure your environment with the required tools to build and run this application. (A restart is required after running the script below.)

##### Install Visual Studio Code and related extensions
```powershell
install-Module -Name Microsoft.WinGet.Configuration -AllowPrerelease -AcceptLicense  -Force
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
Get-WinGetConfiguration -File .\.config\configuration.vsCode.winget | Invoke-WinGetConfiguration -AcceptConfigurationAgreements
```

> Note: These commands may require `sudo`

- Optional: Install [Visual Studio Code with C# Dev Kit](https://code.visualstudio.com/docs/csharp/get-started)
- Optional: Install [.NET MAUI Workload](https://learn.microsoft.com/dotnet/maui/get-started/installation?tabs=visual-studio-code)

> Note: When running on Mac with Apple Silicon (M series processor), Rosetta 2 for grpc-tools. 

### Running the solution

> [!WARNING]
> Remember to ensure that your container runtime is started

- (Windows only) Run the application from Visual Studio:
- Open the `eShop.Web.slnf` file in Visual Studio
- Ensure that `eShop.AppHost.csproj` is your startup project
- Hit Ctrl-F5 to launch Aspire

* Or run the application from your terminal:
```powershell
aspire run
```
`aspire.config.json` points this command to `src/eShop.AppHost/eShop.AppHost.csproj`. This repo also includes test AppHosts, so use `--apphost <path>` when you want to target one explicitly. Then look for lines like this in the console output to find the URL to open the Aspire dashboard:
```sh
Login to the dashboard at: http://localhost:19888/login?t=uniquelogincodeforyou
```

### Running tests

Run the server tests:

```powershell
dotnet test --solution eShop.Web.slnf
```

Run the Playwright browser journeys. Playwright starts the AppHost automatically, so ensure your container runtime is running first.

```powershell
npm ci
npx playwright install chromium
npm run test:e2e
```

### Optional: AI Chatbot with Microsoft Foundry

To use Microsoft Foundry for chat and embeddings, set `UseFoundry=true` in the
AppHost environment. Aspire provisions the Foundry resource and the
`gpt-4.1-mini` and `text-embedding-3-small` deployments, then injects their
connection information into the consuming projects.

The Foundry hosting integration currently uses a preview package. It replaces
the sample's previous direct OpenAI and existing Azure OpenAI configuration paths.

```powershell
$env:UseFoundry = "true"
aspire run
```

See the [Microsoft Foundry Aspire hosting integration](https://aspire.dev/integrations/cloud/azure-ai-foundry/) for configuration and deployment details.

### Deploy with Aspire CLI

Use Aspire deployment from the AppHost model.

This sample intentionally deploys disposable PostgreSQL, Redis, and RabbitMQ containers to Azure Container Apps. It is suitable for evaluation and demonstrations, not production data.

Prerequisites:
- A supported container runtime must be running.
- Azure CLI must be authenticated.
- Azure deployment settings must be set (`Azure__SubscriptionId`, `Azure__Location`, `Azure__ResourceGroup`).

1. Preview deployment steps:
```sh
aspire publish --list-steps
aspire deploy --list-steps
```
1. Publish deployment artifacts for inspection or handoff:
```sh
aspire publish
```
1. Deploy directly from the AppHost model:
```sh
aspire deploy
```

Example (PowerShell):
```powershell
$env:Azure__SubscriptionId = "<subscription-id>"
$env:Azure__Location = "eastus"
$env:Azure__ResourceGroup = "rg-eshop-prod"
aspire deploy --non-interactive
```

`aspire deploy` evaluates the AppHost directly; it does not consume a previous `aspire publish` output directory. Omit `--non-interactive` for interactive use; add it for automation or agent-driven runs so Aspire does not prompt for missing deployment settings. Set the required values explicitly.

## Contributing

For more information on contributing to this repo, read [the contribution documentation](./CONTRIBUTING.md) and [the Code of Conduct](CODE-OF-CONDUCT.md).

### Sample data

The sample catalog data is defined in [catalog.json](https://github.com/dotnet/eShop/blob/main/src/Catalog.API/Setup/catalog.json). Those product names, descriptions, and brand names are fictional and were generated using [GPT-35-Turbo](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt), and the corresponding [product images](https://github.com/dotnet/eShop/tree/main/src/Catalog.API/Pics) were generated using [DALL·E 3](https://openai.com/dall-e-3).

## eShop on Azure

For a version of this app configured for deployment on Azure, please view [the eShop on Azure](https://github.com/Azure-Samples/eShopOnAzure) repo.
