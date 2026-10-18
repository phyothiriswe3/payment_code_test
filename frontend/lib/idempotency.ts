/**
 * Returns the idempotency key for this purchase attempt, creating it on first use.
 * Stored in sessionStorage so a refresh or a repeated click reuses the same key.
 */
export function getOrCreateIdempotencyKey(
  customerId: number,
  productId: number,
  cardId: number | null,
): string {
  const storageKey = `idem:${customerId}:${productId}:${cardId ?? "new"}`;
  const existing = sessionStorage.getItem(storageKey);
  if (existing) return existing;
  const key = crypto.randomUUID();
  sessionStorage.setItem(storageKey, key);
  return key;
}

/** Drops every stored key for a customer/product so the next attempt gets a fresh key. */
export function clearIdempotencyKeys(customerId: number, productId: number) {
  const p = `idem:${customerId}:${productId}:`;
  for (let i = sessionStorage.length - 1; i >= 0; i--) {
    const key = sessionStorage.key(i);
    if (key?.startsWith(p)) sessionStorage.removeItem(key);
  }
}
