import type {
  Card,
  CheckoutRequest,
  CheckoutResponse,
  Customer,
  ListTransactionsQuery,
  PagedResult,
  ProblemDetails,
  Product,
  Transaction,
} from "./types";

export const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080"
).replace(/\/+$/, "");

/** Error carrying the API's ProblemDetails. `status` is 0 when the request never reached the API. */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;

  constructor(status: number, title: string, detail?: string) {
    super(detail ? `${title}: ${detail}` : title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
  }
}

async function toApiError(res: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};
  try {
    problem = (await res.json()) as ProblemDetails;
  } catch {
    // Non-JSON error body; fall back to the status text.
  }
  const validation = problem.errors
    ? Object.values(problem.errors).flat().join(" ")
    : undefined;
  return new ApiError(
    res.status,
    problem.title ?? (res.statusText || "Request failed"),
    problem.detail ?? (validation || undefined),
  );
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      cache: "no-store",
      headers: { Accept: "application/json", ...init.headers },
    });
  } catch {
    throw new ApiError(0, "Network error", "Could not reach the payment API.");
  }
  if (!res.ok) throw await toApiError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const getCustomers = () => request<Customer[]>("/api/customers");

export const getProducts = () => request<Product[]>("/api/products");

export const getCustomerCards = (customerId: number) =>
  request<Card[]>(`/api/customers/${customerId}/cards`);

export const checkout = (body: CheckoutRequest, idempotencyKey: string) =>
  request<CheckoutResponse>("/api/checkout", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    body: JSON.stringify(body),
  });

export function listTransactions({ page, pageSize, status }: ListTransactionsQuery = {}) {
  const qs = new URLSearchParams();
  if (page) qs.set("page", String(page));
  if (pageSize) qs.set("pageSize", String(pageSize));
  if (status) qs.set("status", status);
  const query = qs.toString();
  return request<PagedResult<Transaction>>(`/api/transactions${query ? `?${query}` : ""}`);
}

export const getTransaction = (invoiceNo: string) =>
  request<Transaction>(`/api/transactions/${encodeURIComponent(invoiceNo)}`);

export const confirmTransaction = (invoiceNo: string) =>
  request<Transaction>(
    `/api/transactions/${encodeURIComponent(invoiceNo)}/confirm`,
    { method: "POST" },
  );

/** Backend notification endpoint (normally called by 2C2P, server to server). */
export const postNotification = (payload: string) =>
  request<void>("/api/payments/2c2p/notification", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ payload }),
  });

/** `frontendReturnUrl` target: the API 302-redirects the browser to `/result/{invoiceNo}`. */
export const paymentReturnUrl = (invoiceNo: string) =>
  `${API_BASE_URL}/api/payments/2c2p/return?invoiceNo=${encodeURIComponent(invoiceNo)}`;
