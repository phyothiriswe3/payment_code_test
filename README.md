# Rezerv Checkout: 2C2P integration

A checkout that sells one-time and recurring products through the 2C2P Payment Gateway (PGW v4.3, sandbox).

- **Backend:** ASP.NET Core 8 Web API with Clean Architecture (Domain / Application / Infrastructure / Api), EF Core + Pomelo on MySQL 8, and Redis for locking and caching.
- **Frontend:** Next.js. You pick a customer and a product (and a saved card if the customer has one), pay, and then see the result page.
- **Payments:**
  - New card: the 2C2P hosted payment page. Recurring products ask for tokenization (`tokenize=true`, channel `CC`).
  - Saved card: Payment Token with `customerToken`, then `DoPayment`.
  - Results arrive through the signed backend notification. The Payment Inquiry API is the fallback.

## Prerequisites

- Docker Desktop, which runs the compose stack and the Testcontainers tests
- `cloudflared` (or `ngrok`), so 2C2P can reach the API's notification endpoint
- Local runs without Docker also need the .NET 8 SDK and Node 20

## Quick start (Docker)

```bash
cp .env.example .env
```

1. Open https://developer.2c2p.com/docs/sandbox and copy the secret key of the public Singapore demo merchant **JT01** (SGD) into `TWOC2P_SECRET_KEY` in `.env`. `.env` is gitignored, so never put the key in a tracked file.
2. Start a tunnel to the API. The API serves plain HTTP on 8080:
   ```bash
   cloudflared tunnel --url http://localhost:8080
   ```
   Alternatively, run `docker compose --profile tunnel up cloudflared` and read the URL from its logs.
3. Set `PUBLIC_API_URL` in `.env` to the printed `https://….trycloudflare.com` URL, with no trailing slash. Compose builds two URLs from it:
   - `TwoC2P__BackendReturnUrl` = `${PUBLIC_API_URL}/api/payments/2c2p/notification`
   - `TwoC2P__FrontendReturnUrl` = `${PUBLIC_API_URL}/api/payments/2c2p/return`
4. Start the stack:
   ```bash
   docker compose up --build
   ```
5. Open the shop at http://localhost:3000 and the API docs at http://localhost:8080/swagger.

On startup the API applies the EF migrations and seeds:
- customers 1–3
- products 1–2 (`OneTime`)
- products 3–4 (`Recurring`)

If you change `PUBLIC_API_URL` later, run `docker compose up -d api` so the api container picks up the new value.

### Test cards (2C2P Singapore sandbox)

| Card | Number | CVV | Expiry | OTP |
|---|---|---|---|---|
| Visa | 4111111111111111 | 123 | any future date | 123456 |
| Mastercard | 5555555555554444 | 123 | any future date | 123456 |

The list has no "declined" card. To demo a failed payment, either cancel on the hosted page or enter a wrong OTP.

To save a card, buy a **Recurring** product (3 or 4) on the hosted page. After the successful notification, the card appears under that customer.

## Running locally without Docker

```bash
docker compose up -d mysql redis          # or your own MySQL 8 + Redis 7 on 3306 / 6379
export PATH=$HOME/.dotnet:$PATH
export TwoC2P__SecretKey='<JT01 secret>'
export TwoC2P__BackendReturnUrl='https://<tunnel>/api/payments/2c2p/notification'
export TwoC2P__FrontendReturnUrl='https://<tunnel>/api/payments/2c2p/return'
dotnet run --project backend/src/Checkout.Api          # http://localhost:8080 (launchSettings)
```

The default connection strings in `backend/src/Checkout.Api/appsettings.json` already target `localhost` with the compose credentials.

Start the frontend in another terminal:

```bash
cd frontend
npm ci
npm run dev                               # http://localhost:3000; NEXT_PUBLIC_API_BASE_URL defaults to http://localhost:8080
```

## Project structure

```
backend/
  src/
    Checkout.Domain/          entities (Customer, Product, CustomerCard, PaymentTransaction) + enums
    Checkout.Application/     use cases; depends on abstractions only
      Abstractions/           IPaymentProvider, IDistributedLock, ICacheService, IAppDbContext, IClock, …
      Checkout/               CheckoutService (idempotency), HostedPaymentFlow, SavedCardPaymentFlow
      Payments/               PaymentResultService (notification/inquiry), TransactionQueryService
      Catalog/                CatalogService (cache-aside reads)
    Checkout.Infrastructure/  EF Core/MySQL (configs, migrations, seed), Redis lock + cache,
                              2C2P client (Payments/TwoC2P: JWT HS256, wire models)
    Checkout.Api/             controllers, ProblemDetails mapping, Swagger
  tests/
    Checkout.UnitTests/
    Checkout.IntegrationTests/  WebApplicationFactory + Testcontainers (MySQL, Redis), fake 2C2P
frontend/                     Next.js app (app/, components/, lib/)
docker-compose.yml            mysql, redis, api, web, optional cloudflared (profile "tunnel")
```

### API

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/customers` | List customers (cached) |
| GET | `/api/customers/{id}/cards` | List a customer's saved cards (cached) |
| GET | `/api/products` | List products (cached) |
| POST | `/api/checkout` | Start a payment. Needs an `Idempotency-Key` header (UUID). |
| GET | `/api/transactions?page=&pageSize=&status=` | Paginated transaction records for review, newest first (pageSize 1–100, optional status filter, never cached) |
| GET | `/api/transactions/{invoiceNo}` | Read transaction status (read from MySQL every time) |
| POST | `/api/transactions/{invoiceNo}/confirm` | Run a Payment Inquiry if the transaction is still Pending |
| POST | `/api/payments/2c2p/notification` | 2C2P backend notification (`{ "payload": "<JWT>" }`) |
| GET/POST | `/api/payments/2c2p/return` | 2C2P browser return. Redirects to `/result/{invoiceNo}` on the frontend. |

Errors are returned as RFC 7807 ProblemDetails:

| Status | Meaning |
|---|---|
| 400 | Validation failure or bad signature |
| 404 | Not found |
| 409 | Checkout with this key is still in progress |
| 422 | Idempotency key reused with a different body |

## Payment flows

### Hosted payment page (new card; tokenized for Recurring)

```mermaid
sequenceDiagram
  participant B as Browser
  participant W as Web (Next.js)
  participant A as API
  participant R as Redis
  participant D as MySQL
  participant P as 2C2P
  B->>W: choose customer + product, Pay
  W->>A: POST /api/checkout (Idempotency-Key)
  A->>R: SET lock:checkout:{key} NX PX
  A->>D: INSERT transaction (Pending, unique idempotency_key)
  A->>P: Payment Token (JWT; tokenize=true for Recurring)
  P-->>A: paymentToken + webPaymentUrl
  A->>D: save redirect_url, processed_at
  A-->>W: { invoiceNo, Pending, redirectUrl }
  W->>B: redirect to hosted page
  B->>P: card + OTP
  P->>A: POST /notification { payload JWT }
  A->>A: verify HS256 signature, amount + currency
  A->>D: UPDATE … WHERE status='Pending' (+ insert customer_card if customerToken)
  P->>A: browser return (/return)
  A-->>B: 302 → /result/{invoiceNo}
  B->>A: poll GET /api/transactions/{invoiceNo}
  Note over B,A: still Pending after 30 s → POST /confirm (Payment Inquiry), retried every 30 s while Pending
```

### Saved card

```mermaid
sequenceDiagram
  participant W as Web
  participant A as API
  participant D as MySQL
  participant P as 2C2P
  W->>A: POST /api/checkout { customerCardId } (Idempotency-Key)
  A->>D: card belongs to customer? INSERT Pending transaction
  A->>P: Payment Token (customerToken, customerTokenOnly, CC)
  P-->>A: paymentToken
  A->>P: DoPayment (paymentToken, customerToken)
  alt 2000 (completed)
    A->>P: Payment Inquiry
    A->>D: UPDATE … WHERE status='Pending' → Succeeded/Failed
  else 1001 (3DS required)
    A-->>W: redirectUrl → browser completes 3DS, then notification as in hosted flow
  else non-final code (e.g. 5002, 0001)
    A->>D: stay Pending; notification or /confirm settles it
  else final decline
    A->>D: Failed
  else error/timeout after DoPayment started
    A->>D: stay Pending (never assume failure); /confirm resolves it
  end
  A-->>W: { invoiceNo, status, redirectUrl? }
```

The saved-card flow never marks a transaction Succeeded from the DoPayment response alone. Success is always confirmed through the Inquiry API or the signed notification. The result page runs `/confirm` immediately for saved-card payments, then every 30 s while the transaction is still Pending.

In the hosted flow nothing is charged during the Payment Token step, so any provider error there (network, timeout, bad signature, unparseable response) marks the row Failed and settles the key; it never stays stuck on 409.

## Idempotency and concurrency (`CheckoutService`)

The goal is that exactly one request per `Idempotency-Key` reaches 2C2P. The frontend keeps one UUID per customer/product/card in `sessionStorage`, so double clicks and refreshes reuse the key. It clears the key once the result is final.

1. **Validate and fingerprint.**
   - The key must be a UUID and is normalized to lowercase.
   - Customer, product and card ownership are checked.
   - `request_fingerprint = SHA-256(customerId|productId|cardId)`.
2. **Fast path.** If a row with this key already exists:
   - Settled (`processed_at` set): its result is replayed without touching Redis or 2C2P.
   - Different fingerprint: **422**.
3. **Redis lock.** `SET lock:checkout:{key} <owner> NX PX 30000`. It is released by a Lua compare-and-delete script, so an expired owner cannot delete someone else's lock.
   - If another request holds the lock, this request *waits*. It polls MySQL every 200 ms for a settled row for up to 10 s (`App:CheckoutWaitSeconds`), and returns **409** after that. The frontend retries 409s.
4. **Double-check under the lock.**
   - A row that is now settled is replayed.
   - A row that exists but is *unsettled* means its owner crashed or its lock expired mid-call. The answer is **409**. The API never blindly re-sends the charge.
5. **Insert** the Pending row. The unique index `ux_transaction_idempotency_key` makes this insert the real claim on the key.
6. **Provider call**, then set `processed_at`. This is the settle marker that releases waiting duplicates and makes the row replayable.

**When Redis is down**, the lock call throws. The request continues without a lock, and the MySQL unique index still allows only one insert. The loser catches `DbUpdateException`, finds the winner's row and waits for it to settle.

Why both guards:
- The lock turns concurrent duplicates into cheap waits.
- The database index is the guarantee that holds even when Redis is unavailable or a lock expires.

## Caching

The cache is Redis cache-aside (`ICacheService`). Every entry has a 10-minute TTL.

| Key | Content | Invalidation |
|---|---|---|
| customers | customer list | TTL only (seed data) |
| products | product list | TTL only (seed data) |
| cards:{customerId} | saved cards of one customer | Deleted whenever a payment result carries a `customerToken` |

**Transaction status is never cached.** It is changed by notifications, inquiries and other API instances, and the result page polls it. A stale "Pending" or a stale "Failed" would be wrong in a way users notice, and a primary-key lookup is already cheap.

## Notification handling (`PaymentResultService`)

1. **Signature.** The `payload` JWT is verified with HS256 and the merchant secret. An invalid signature returns 400, and nothing is written.
2. **Unknown invoice.** The result is logged and ignored.
3. **Amount and currency.** Both must match the stored transaction (the price always comes from the product row, never from the client). A mismatch is logged as a security warning and ignored. When the merchant settles in a different currency, 2C2P converts the payment and returns the requested amount in `fxAmount`/`fxCurrencyCode`, with the settlement amount in `amount`/`currencyCode`. The sandbox merchant `JT01` does this: it settles in THB, so 25.00 SGD arrives as `amount: 605.88, currencyCode: "THB", fxAmount: 25.0, fxCurrencyCode: "SGD"`. The 2C2P provider then compares the FX pair against the order.
4. **Non-final codes stay Pending:** `0001, 0999, 1001, 2000, 2001, 2002, 4009, 4071, 5002, 5998` (`PaymentResponseCodes`, shared with the saved-card DoPayment step). Marking them Failed would be permanent and would hide a later `0000`.
5. **Conditional update.** `0000` sets Succeeded (channel, masked card, tranRef, paid_at). Any other final code sets Failed. Both run as `UPDATE … WHERE id = @id AND status = 'Pending'`, so the first final result wins and duplicate or late notifications change nothing.
6. **Card saved once.** On success with a `customerToken`, the card is inserted unless it already exists. The unique index `(customer_id, customer_token)` absorbs concurrent duplicates. This step runs even if the update affected no rows, so a notification retried after a crash still saves the card. The customer's card cache is then invalidated.

The same `ApplyAsync` path processes the result of `POST /confirm` (Payment Inquiry).

## Data model choices

The tables are `customer`, `product`, `customer_card` and `transaction`. Beyond the required columns, `transaction` also has:

| Column | Why |
|---|---|
| `idempotency_key` (unique) | The database-level duplicate guard |
| `request_fingerprint` | Detects a key reused with a different body (422) |
| `flow` (HostedPage / SavedCard) | The result page and inquiry behave differently per flow |
| `payment_token`, `redirect_url` | Lets a replay return the same hosted-page URL without calling 2C2P again |
| `customer_card_id` (nullable FK) | Records which saved card was charged |
| `resp_code`, `resp_desc`, `provider_ref`, `payment_channel`, `masked_card_no`, `paid_at` | Audit trail and support |
| `updated_at` | Shows when the row last changed |
| `processed_at` | Settle marker: a replay only returns rows where the provider call has finished |

The amount is stored per transaction (copied from the product at checkout time), so later price changes don't rewrite history.

`customer_card` stores only the 2C2P `customerToken` and the masked number. It never stores a PAN or CVV.

## Tests

Docker must be running: the integration tests start MySQL and Redis with Testcontainers.

```bash
export PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet test backend/Checkout.sln
```

- **Unit tests:** 2C2P JWT signing and verification, provider request/response mapping.
- **Integration tests:** the API against real MySQL and Redis with a fake `IPaymentProvider`. They cover:
  - concurrent checkouts with the same key (one provider call)
  - 422 on a fingerprint mismatch
  - notification signature, amount check and duplicate handling
  - saving a card exactly once

## Assumptions

- **Merchant:** the single public sandbox merchant JT01, with SGD (702) only. Product prices are in SGD.
- **Tokenization:** JT01 is assumed to support tokenization. If the hosted page offers no save-card option, a dedicated sandbox merchant is needed.
- **No authentication:** the customer is chosen in the UI, and the API checks that a saved card belongs to the chosen customer.
- **Recurring products:** "Recurring" means the card is tokenized for later one-click charges. There is no scheduler that charges automatically.
- **Tunnel:** 2C2P must reach the API through a public HTTPS tunnel. Without one, payments stay Pending until the result page triggers `/confirm` (Payment Inquiry).

## What I would improve

- **Durable notification processing:** persist raw notifications (inbox/outbox) and process them from a queue, with retries.
- **Reconciliation:** a background job that runs Payment Inquiry for transactions left Pending, including rows abandoned after a crash (which currently answer 409).
- **Locking:** renew the lock (watchdog) for long provider calls instead of relying on a fixed 30 s TTL.
- **Refunds and voids** through `IPaymentProvider`.
- **Replaying error responses:** store the error response alongside the request hash, so a retry with the same key replays it exactly.
- **Observability:** OpenTelemetry traces and metrics, structured log correlation by invoice number, and health checks.
- **Security hardening:** rate limiting on checkout and the notification endpoint, and real customer authentication.
