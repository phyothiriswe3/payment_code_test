import { formatMoney, paymentTypeLabel } from "@/lib/format";
import type { Customer, Product } from "@/lib/types";

export default function PaymentSummary({
  customer,
  product,
}: {
  customer?: Customer;
  product?: Product;
}) {
  const rows: [string, string][] = [
    ["Customer", customer ? `${customer.firstName} ${customer.lastName}` : "—"],
    ["Product", product?.name ?? "—"],
    ["Payment type", product ? paymentTypeLabel(product.paymentType) : "—"],
  ];

  return (
    <section className="rounded-lg border bg-muted/40 p-4">
      <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Payment summary
      </h2>
      <dl className="space-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="text-right font-medium">{value}</dd>
          </div>
        ))}
      </dl>
      <div className="mt-3 flex items-baseline justify-between gap-4 border-t pt-3">
        <span className="text-sm text-muted-foreground">Total</span>
        <span className="text-lg font-semibold tabular-nums">
          {product ? formatMoney(product.price, product.currency) : "—"}
        </span>
      </div>
    </section>
  );
}
