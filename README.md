# National Digital Banking Portal — Enterprise Banking & Payment Gateway Architecture

A full-stack digital banking and payment gateway platform built on .NET 10 and
[Aspire](https://aspire.dev/), evolved from the eShop reference application into an
enterprise-grade financial system implementing **ISO 20022**, **SWIFT**, **PCI-DSS**,
**PSD2**, and **AML** controls end to end.

![National Digital Banking Portal architecture diagram](img/eshop_architecture.png)

## Platform Capabilities

| Capability | Implementation |
| ---------- | -------------- |
| Financial Accounts & Card dashboard | Blazor portal home page; PANs masked per PCI-DSS 3.3 (last-4 only) |
| Payment Transfer & SWIFT/ISO 20022 Gateway | `/checkout` portal page: beneficiary name, IBAN, SWIFT/BIC, amount, live pacs.008 message preview |
| Compliance badges | Portal header: PCI-DSS Certified, ISO 20022 Compliant, Encrypted Session |
| ISO 20022 messaging | `pain.001.001.09` (customer initiation) and `pacs.008.001.08` (interbank settlement) DTOs with real XML element names |
| SWIFT formatting | ISO 9362 BIC and ISO 13616 IBAN validation, normalization, display grouping, and masking (`SwiftFormatting`) |
| PCI-DSS encryption | Field-level AES-256-GCM encryption at rest for PANs, IBANs, and account IDs via EF Core value converters |
| Idempotency | `x-requestid` Idempotency-Key tokens on all payment endpoints, stored atomically in Redis (memory cache fallback) — double-spend prevention |
| ACID transactions | Explicit EF Core transactions through the Npgsql execution strategy; serializable isolation for monetary commands |
| Resilience | Polly pipelines for monetary balance reads and transient-failure retries |
| AML / PSD2 audit trail | Immutable, SHA-256 hash-chained `financial_audit_events` ledger written inside the same DB transaction |
| Container security | All service images run as non-root user (UID 1654) |
| CI/CD | `banking-ci.yml` — workload restore, build, payment-flow unit tests, Postgres-backed functional tests, NuGet vulnerability scan, DevSkim, CodeQL |

## Architecture

```text
WebApp (Blazor banking portal)
  ├─ Accounts & Cards dashboard        (masked PANs, balances)
  ├─ Payment Transfer Gateway          (IBAN / SWIFT / beneficiary / amount)
  └─ Transaction history               (order-status event stream)
        │
        ▼  HTTPS, x-requestid idempotency header
Ordering.API  ── Payment.Shared (ISO 20022 / AES-256-GCM / idempotency / audit)
  ├─ IdempotencyEndpointFilter  → Redis token reservation (24h TTL, atomic set-if-absent)
  ├─ TransactionBehavior        → serializable EF Core transactions (monetary commands)
  ├─ IdentifiedCommand pipeline → second-layer per-command dedup
  └─ SetPaidOrderStatusCommand  → pacs.008 settlement message + hash-chained audit event
        │
        ▼  RabbitMQ integration events
PaymentProcessor ── pain.001 authorization message + structured AML audit logs
```

## Banking Standards Detail

### ISO 20022 & SWIFT

- `src/Payment.Shared/Iso20022/` models `pain.001.001.09` and `pacs.008.001.08`
  with end-to-end IDs (Max35Text), instructed amounts with ISO 4217 currency,
  debtor/creditor parties, agent BICs, and charge bearer — serialized with the
  official ISO 20022 XML element names.
- `Iso20022PaymentMessageFactory` rejects any instruction whose debtor/creditor
  agent BIC fails ISO 9362 validation before a message can be constructed.
- `SwiftFormatting` provides IBAN normalization, four-character display grouping,
  and masking (`DE89XXXXXXXXXXXXXX3000`) for logs and UI.
- The portal's transfer gateway shows a live pacs.008 preview (End-to-End ID,
  debtor, creditor, IBAN, BIC, amount, idempotency key) before authorization.

### PCI-DSS Data Protection

- `Aes256SensitiveDataProtector` encrypts card numbers, security codes, and account
  identifiers with AES-256-GCM before persistence (EF Core value converter in
  `PaymentMethodEntityTypeConfiguration`). Keys come from configuration
  (`PaymentSecurity:EncryptionKey`, base64 256-bit), never from source control.
- `PanMasking` ensures only the last four digits ever appear in logs, API payloads,
  or the UI; decryption failures surface as uniform `CryptographicException`s to
  prevent padding-oracle behavior.

### Idempotent Transaction Handling

- Every financial endpoint requires an `x-requestid` GUID header.
- `IdempotencyEndpointFilter` atomically reserves the token in Redis
  (`SET NX` + 24h TTL); concurrent duplicates receive HTTP 200 with
  `X-Idempotent-Replay: true` instead of re-executing.
- The MediatR `IdentifiedCommand` pipeline keeps per-command `RequestManager`
  deduplication as a second safety layer.

### ACID & Resilience

- `TransactionBehavior` wraps each command in an explicit transaction created via
  the Npgsql EF Core execution strategy (retry-safe), using **serializable**
  isolation for monetary commands and read-committed otherwise.
- Polly resilience pipelines (`DatabaseResiliencePipelines`) retry monetary balance
  reads with exponential backoff.

### AML / PSD2 Audit Trail

- Every financial event (order placement, authorization, settlement, cancellation)
  appends a `FinancialAuditEvent` to `ordering.financial_audit_events` inside the
  same ACID transaction as the state change.
- Entries carry event type, UTC timestamp, correlation id, actor, subject,
  amount/currency, outcome, ISO 20022 message id, and a SHA-256 hash chain making
  the ledger tamper-evident.

## Getting Started

### Prerequisites

- Clone this repository.
- Install the latest [.NET 10 SDK](https://dot.net/download).
- Install and start an OCI-compatible container runtime, such as
  [Docker Desktop](https://docs.docker.com/engine/install/) or [Podman](https://podman.io/)
  (required for Postgres, Redis, and RabbitMQ which Aspire orchestrates).
- Optional (Windows): Visual Studio 2022 17.10+ with the `ASP.NET and web
  development` workload and `Aspire SDK` component.

### Running the solution

> [!WARNING]
> Ensure your container runtime is started first.

```powershell
aspire run
```

`aspire.config.json` targets `src/eShop.AppHost/eShop.AppHost.csproj`. The Aspire
dashboard URL is printed on startup (`Login to the dashboard at: http://localhost:19888/...`).

### Running tests

```powershell
dotnet test --solution eShop.Web.slnf
```

Payment-flow suites: `tests/Ordering.UnitTests` (PCI-DSS, SWIFT/ISO 20022,
idempotency), `tests/Application.UnitTests` (PaymentProcessor), and the
Postgres-backed `tests/Ordering.FunctionalTests`.

### Configuration

| Setting | Purpose |
| ------- | ------- |
| `PaymentSecurity:EncryptionKey` | Base64-encoded 256-bit AES key for field-level encryption (optional; a dev-only no-op protector is used without it) |
| `ConnectionStrings:redis` | Redis instance backing the idempotency token store |

## CI/CD

`.github/workflows/banking-ci.yml` runs on every payment-path change:

1. `dotnet workload restore` (MAUI/Tizen workloads) → `dotnet restore` → Release build.
2. Payment-flow unit tests (`Ordering.UnitTests`, `Application.UnitTests`,
   AppHost orchestration tests).
3. Postgres-backed ordering functional tests (migrations, API, audit trail).
4. Security linters: NuGet vulnerability scan, DevSkim, CodeQL.

Service images are published as non-root containers (`ContainerUser=1654`).

## Repository Layout

- `src/WebApp` — Blazor banking portal (accounts dashboard, transfer gateway, transaction history)
- `src/Ordering.API` / `src/Ordering.Infrastructure` — transfer processing, idempotency, transactions, audit ledger
- `src/Payment.Shared` — ISO 20022 messages, SWIFT formatting, AES-256-GCM, idempotency stores, audit abstractions
- `src/PaymentProcessor` — authorization worker emitting pain.001 messages
- `src/eShop.AppHost` — Aspire orchestration (Postgres, Redis, RabbitMQ, services)
- `.github/workflows/banking-ci.yml` — banking compliance pipeline

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This project is a reference architecture
for evaluation purposes; it demonstrates banking controls but is not certified for
production financial use without a formal PCI-DSS assessment.
