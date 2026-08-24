# BankApp — Repository Notes

## What this is

National Digital Banking Portal (evolved from the .NET eShop reference app). Only
the banking/payment flow is kept: WebApp (Blazor portal) → Ordering.API →
OrderProcessor → PaymentProcessor, with Identity.API for auth, Payment.Shared for
ISO 20022 / AES-256-GCM / idempotency, Postgres + Redis + RabbitMQ via Aspire.

All eShop storefront pieces (Basket.API, Catalog.API, Webhooks, MAUI HybridApp/
ClientApp, WebAppComponents, Playwright e2e, AI chatbot/Ollama/Foundry) were
removed in Aug 2026. Do not reintroduce them.

## Key invariants

- OrderProcessor is required: it publishes GracePeriodConfirmedIntegrationEvent,
  which drives orders Submitted → payment authorization (PaymentProcessor handles
  OrderStatusChangedToStockConfirmed). Deleting it silently stalls transfers.
- The portal submits a transfer as an order with a single synthesized line item
  priced at the transfer amount (see WebApp/Services/TransferService.cs); the
  Ordering backend requires non-empty order items and totals must match the
  transfer amount for the AML audit trail.
- Identity.API Config.cs must only define the "orders" scope and the webapp /
  orderingswaggerui clients; IdentityConfigurationTests asserts this.

## Build & test (this environment)

- .NET SDK is not preinstalled; it was installed to /tmp/dotnet via
  https://dot.net/v1/dotnet-install.sh --channel 10.0 (export PATH=/tmp/dotnet:$PATH).
- Build: `dotnet build eShop.Web.slnf` (slnf and eShop.slnx contain identical sets).
- Unit tests: Ordering.UnitTests (82), Application.UnitTests (8) — no Docker needed.
- Ordering.FunctionalTests (11) need Docker: `sudo dockerd > /tmp/docker.log 2>&1 &`
  then `sudo chmod 666 /var/run/docker.sock`.
