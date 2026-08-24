# Banking Portal Tests

This directory contains unit and functional tests validating the payment flow of
the National Digital Banking Portal: Ordering command handlers, PCI-DSS
encryption, SWIFT/ISO 20022 messaging, idempotency, and the PaymentProcessor
worker.

**NOTE:** Functional tests in this leverage the Aspire host to spin up test containers and require that Docker be running as a pre-requisite.
