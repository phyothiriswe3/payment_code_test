"use client";

import { AlertCircle, CreditCard, Loader2, Plus } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import PaymentSummary from "@/components/PaymentSummary";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  ApiError,
  checkout,
  getCustomerCards,
  getCustomers,
  getProducts,
} from "@/lib/api";
import { getOrCreateIdempotencyKey } from "@/lib/idempotency";
import type { Card, Customer, Product } from "@/lib/types";

const MAX_CONFLICT_RETRIES = 5;
const CONFLICT_RETRY_DELAY_MS = 1000;

const asApiError = (e: unknown) =>
  e instanceof ApiError ? e : new ApiError(0, "Unexpected error", String(e));

export default function CheckoutForm({
  initialCustomerId,
  initialProductId,
}: {
  initialCustomerId?: number;
  initialProductId?: number;
}) {
  const router = useRouter();
  const [customers, setCustomers] = useState<Customer[] | null>(null);
  const [products, setProducts] = useState<Product[] | null>(null);
  const [customerId, setCustomerId] = useState<number | null>(null);
  const [productId, setProductId] = useState<number | null>(null);
  const [cards, setCards] = useState<{ customerId: number; list: Card[] } | null>(null);
  const [cardId, setCardId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([getCustomers(), getProducts()])
      .then(([c, p]) => {
        if (!active) return;
        setCustomers(c);
        setProducts(p);
        setCustomerId(c.some((x) => x.id === initialCustomerId) ? initialCustomerId! : (c[0]?.id ?? null));
        setProductId(p.some((x) => x.id === initialProductId) ? initialProductId! : (p[0]?.id ?? null));
      })
      .catch((e) => active && setError(asApiError(e)));
    return () => {
      active = false;
    };
  }, [initialCustomerId, initialProductId]);

  useEffect(() => {
    if (customerId === null) return;
    let active = true;
    getCustomerCards(customerId)
      .then((list) => active && setCards({ customerId, list }))
      .catch((e) => active && setError(asApiError(e)));
    return () => {
      active = false;
    };
  }, [customerId]);

  const customer = customers?.find((c) => c.id === customerId);
  const product = products?.find((p) => p.id === productId);
  const customerCards = cards?.customerId === customerId ? cards.list : null;
  const selectedCardId = customerCards?.some((c) => c.id === cardId) ? cardId : null;

  async function onCheckout() {
    if (customerId === null || productId === null) return;
    setSubmitting(true);
    setError(null);
    const key = getOrCreateIdempotencyKey(customerId, productId, selectedCardId);
    const body = { customerId, productId, customerCardId: selectedCardId };

    for (let attempt = 0; ; attempt++) {
      try {
        const res = await checkout(body, key);
        // Keep the button disabled until the browser leaves the page.
        if (res.redirectUrl) window.location.href = res.redirectUrl;
        else router.push(`/result/${encodeURIComponent(res.invoiceNo)}`);
        return;
      } catch (e) {
        const err = asApiError(e);
        // 409: another request with this key is still being processed; retry with the same key.
        if (err.status === 409 && attempt < MAX_CONFLICT_RETRIES) {
          const { promise, resolve } = Promise.withResolvers<void>();
          setTimeout(resolve, CONFLICT_RETRY_DELAY_MS);
          await promise;
          continue;
        }
        setError(err);
        setSubmitting(false);
        return;
      }
    }
  }

  const loading = customers === null || products === null;
  const optionClass =
    "flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-2.5 text-sm transition-colors hover:bg-muted/50 has-data-[state=checked]:border-primary has-data-[state=checked]:bg-muted/40 has-disabled:cursor-not-allowed has-disabled:opacity-60";

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="customer">Customer</Label>
          <Select
            disabled={loading || submitting}
            value={customerId === null ? "" : String(customerId)}
            onValueChange={(v) => {
              setCustomerId(Number(v));
              setCardId(null);
            }}
          >
            <SelectTrigger id="customer" className="w-full">
              <SelectValue placeholder={loading ? "Loading…" : "Select a customer"} />
            </SelectTrigger>
            <SelectContent>
              {customers?.map((c) => (
                <SelectItem key={c.id} value={String(c.id)}>
                  {c.firstName} {c.lastName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="product">Product</Label>
          <Select
            disabled={loading || submitting}
            value={productId === null ? "" : String(productId)}
            onValueChange={(v) => setProductId(Number(v))}
          >
            <SelectTrigger id="product" className="w-full">
              <SelectValue placeholder={loading ? "Loading…" : "Select a product"} />
            </SelectTrigger>
            <SelectContent>
              {products?.map((p) => (
                <SelectItem key={p.id} value={String(p.id)}>
                  {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <PaymentSummary customer={customer} product={product} />

      <div className="space-y-3">
        <p className="text-sm font-medium">Payment method</p>
        {customerCards === null && customerId !== null && (
          <div className="space-y-2" aria-label="Loading saved cards">
            <Skeleton className="h-11 w-full" />
          </div>
        )}
        <RadioGroup
          aria-label="Payment method"
          disabled={submitting}
          value={selectedCardId === null ? "new" : String(selectedCardId)}
          onValueChange={(v) => setCardId(v === "new" ? null : Number(v))}
          className="gap-2"
        >
          {customerCards?.map((c) => (
            <label key={c.id} className={optionClass}>
              <RadioGroupItem value={String(c.id)} />
              <CreditCard className="size-4 text-muted-foreground" />
              <span className="font-mono">{c.maskedCardNo}</span>
              <span className="ml-auto text-xs text-muted-foreground">Saved card</span>
            </label>
          ))}
          <label className={optionClass}>
            <RadioGroupItem value="new" />
            <Plus className="size-4 text-muted-foreground" />
            <span>Pay with a new payment method</span>
          </label>
        </RadioGroup>
      </div>

      {error && (
        <div
          role="alert"
          className="flex gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2.5 text-sm text-destructive"
        >
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          <div>
            <p className="font-medium">{error.title}</p>
            {error.detail && <p className="text-destructive/90">{error.detail}</p>}
          </div>
        </div>
      )}

      <Button
        type="button"
        size="lg"
        className="w-full"
        onClick={onCheckout}
        disabled={loading || submitting || !customer || !product}
      >
        {submitting && <Loader2 className="animate-spin" />}
        {submitting ? "Processing…" : "Checkout"}
      </Button>
    </div>
  );
}
