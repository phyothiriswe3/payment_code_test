import type { PaymentType } from "./types";

/** Currency amount, e.g. "SGD 25.00"; falls back to "25.00 XYZ" for codes Intl does not know. */
export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat("en-SG", {
      style: "currency",
      currency,
      currencyDisplay: "code",
    }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

/**
 * Local date and time, e.g. "17 Oct 2026, 14:05".
 * The API serialises UTC timestamps with a trailing "Z".
 */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export const paymentTypeLabel = (type: PaymentType) =>
  type === "Recurring" ? "Recurring" : "One-time";
