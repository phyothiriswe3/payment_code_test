export type TransactionStatus = "Pending" | "Succeeded" | "Failed";
export type PaymentFlow = "HostedPage" | "SavedCard";
export type PaymentType = "OneTime" | "Recurring";

export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
}

export interface Product {
  id: number;
  name: string;
  description: string | null;
  price: number;
  currency: string;
  paymentType: PaymentType;
}

export interface Card {
  id: number;
  maskedCardNo: string;
}

export interface CheckoutRequest {
  customerId: number;
  productId: number;
  customerCardId?: number | null;
}

export interface CheckoutResponse {
  invoiceNo: string;
  status: TransactionStatus;
  redirectUrl: string | null;
}

export interface Transaction {
  invoiceNo: string;
  status: TransactionStatus;
  amount: number;
  currency: string;
  customerId: number;
  productId: number;
  productName: string;
  flow: PaymentFlow;
  paymentChannel: string | null;
  maskedCardNo: string | null;
  respCode: string | null;
  respDesc: string | null;
  createdAt: string;
  paidAt: string | null;
  customerName: string;
  customerEmail: string;
  paymentType: PaymentType;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ListTransactionsQuery {
  page?: number;
  pageSize?: number;
  status?: TransactionStatus;
}

/** RFC 7807 body as returned by the API (validation errors add `errors`). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
