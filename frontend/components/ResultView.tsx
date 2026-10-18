"use client";

import { ArrowLeft, CircleAlert, CircleCheck, List, Loader2, Printer, RotateCcw } from "lucide-react";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { StatusBadge } from "@/components/status-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ApiError, confirmTransaction, getTransaction } from "@/lib/api";
import { formatDateTime, formatMoney, paymentTypeLabel } from "@/lib/format";
import { clearIdempotencyKeys } from "@/lib/idempotency";
import type { Transaction } from "@/lib/types";

const POLL_INTERVAL_MS = 3000;
/** While Pending, run a Payment Inquiry (/confirm) at most this often; hosted flow waits this long before the first one. */
const CONFIRM_INTERVAL_MS = 30_000;

export default function ResultView({ invoiceNo }: { invoiceNo: string }) {
  const [tx, setTx] = useState<Transaction | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const lastConfirmAt = useRef<number | null>(null);

  useEffect(() => {
    let active = true;
    let timer: number | undefined;
    const startedAt = Date.now();

    async function tick() {
      try {
        let next = await getTransaction(invoiceNo);
        // SavedCard confirms immediately; hosted waits for the notification first. Retried every 30s while Pending.
        const lastAt = lastConfirmAt.current ?? (next.flow === "SavedCard" ? -Infinity : startedAt);
        if (next.status === "Pending" && Date.now() - lastAt >= CONFIRM_INTERVAL_MS) {
          next = await confirmTransaction(invoiceNo);
          lastConfirmAt.current = Date.now();
        }
        if (!active) return;
        setTx(next);
        setError(null);
        if (next.status === "Pending") timer = window.setTimeout(tick, POLL_INTERVAL_MS);
        // Settled either way: the next purchase of this product must use a fresh key.
        else clearIdempotencyKeys(next.customerId, next.productId);
      } catch (e) {
        if (!active) return;
        const err = e instanceof ApiError ? e : new ApiError(0, "Unexpected error", String(e));
        setError(err);
        // A missing transaction won't appear later; anything else is retried.
        if (err.status !== 404) timer = window.setTimeout(tick, POLL_INTERVAL_MS);
      }
    }

    tick();
    return () => {
      active = false;
      window.clearTimeout(timer);
    };
  }, [invoiceNo]);

  if (!tx) {
    return error?.status === 404 ? (
      <Card>
        <CardHeader>
          <h1 className="text-xl font-semibold">Transaction not found</h1>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-muted-foreground">No payment exists for invoice {invoiceNo}.</p>
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link href="/">
                <ArrowLeft />
                Back to checkout
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href="/transactions">
                <List />
                View all transactions
              </Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    ) : (
      <Card aria-busy="true">
        <CardHeader className="flex flex-col items-center gap-3 py-8 text-center">
          <Loader2 className="size-8 animate-spin text-muted-foreground" />
          <p className="text-lg font-medium">Loading receipt</p>
          <ErrorLine error={error} />
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-6 w-1/2" />
          <Skeleton className="h-24 w-full" />
        </CardContent>
      </Card>
    );
  }

  return <Receipt tx={tx} error={error} />;
}

const headlines = {
  Succeeded: {
    title: "Payment succeeded",
    icon: <CircleCheck className="size-6 text-emerald-600 dark:text-emerald-400" />,
  },
  Failed: {
    title: "Payment failed",
    icon: <CircleAlert className="size-6 text-red-600 dark:text-red-400" />,
  },
  Pending: {
    title: "Processing payment",
    icon: <Loader2 className="size-6 animate-spin text-amber-600 dark:text-amber-400" />,
  },
} as const;

function Receipt({ tx, error }: { tx: Transaction; error: ApiError | null }) {
  const { title, icon } = headlines[tx.status];
  const total = formatMoney(tx.amount, tx.currency);
  const method = [tx.paymentChannel, tx.maskedCardNo].filter(Boolean).join(" · ");

  return (
    <Card className="print:shadow-none print:ring-0">
      <CardHeader className="gap-6 border-b">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-2xl font-bold tracking-tight">Rezerv</p>
            <p className="text-xs text-muted-foreground">Payment receipt</p>
          </div>
          <StatusBadge status={tx.status} />
        </div>
        <div className="flex items-center gap-3" aria-live="polite">
          {icon}
          <div>
            <h1 className="text-xl font-semibold">{title}</h1>
            {tx.status === "Pending" ? (
              <p className="text-sm text-muted-foreground">This page updates automatically.</p>
            ) : (
              <p className="text-sm text-muted-foreground">
                {tx.status === "Succeeded" ? "Amount paid" : "Amount due"}:{" "}
                <span className="font-medium text-foreground">{total}</span>
              </p>
            )}
            <ErrorLine error={error} />
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <Field label="Invoice No">
            <span className="font-mono">{tx.invoiceNo}</span>
          </Field>
          <Field label="Date issued">{formatDateTime(tx.createdAt)}</Field>
          <Field label="Billed to">
            <span className="block">{tx.customerName}</span>
            <span className="block text-muted-foreground">{tx.customerEmail}</span>
          </Field>
          <Field label="Paid at">{tx.paidAt ? formatDateTime(tx.paidAt) : "—"}</Field>
        </dl>

        <div className="overflow-hidden rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/50 hover:bg-muted/50">
                <TableHead className="px-3">Item</TableHead>
                <TableHead className="px-3">Type</TableHead>
                <TableHead className="px-3 text-right">Qty</TableHead>
                <TableHead className="px-3 text-right">Unit price</TableHead>
                <TableHead className="px-3 text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow className="hover:bg-transparent">
                <TableCell className="px-3 font-medium">{tx.productName}</TableCell>
                <TableCell className="px-3">{paymentTypeLabel(tx.paymentType)}</TableCell>
                <TableCell className="px-3 text-right tabular-nums">1</TableCell>
                <TableCell className="px-3 text-right tabular-nums">{total}</TableCell>
                <TableCell className="px-3 text-right tabular-nums">{total}</TableCell>
              </TableRow>
            </TableBody>
            <TableFooter>
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={4} className="px-3 text-right text-muted-foreground">
                  Subtotal
                </TableCell>
                <TableCell className="px-3 text-right tabular-nums">{total}</TableCell>
              </TableRow>
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={4} className="px-3 text-right font-semibold">
                  Total ({tx.currency})
                </TableCell>
                <TableCell className="px-3 text-right text-base font-semibold tabular-nums">
                  {total}
                </TableCell>
              </TableRow>
            </TableFooter>
          </Table>
        </div>

        <dl className="grid gap-4 text-sm sm:grid-cols-2">
          <Field label="Payment method">{method || "—"}</Field>
          {tx.status === "Failed" && (
            <Field label="Reason">
              <span className="text-red-700 dark:text-red-400">
                {tx.respDesc ?? "The payment could not be completed."}
              </span>
            </Field>
          )}
        </dl>
      </CardContent>

      <CardFooter className="flex flex-wrap gap-2 py-4 print:hidden">
        {tx.status === "Failed" ? (
          <Button asChild>
            <Link href={`/?customerId=${tx.customerId}&productId=${tx.productId}`}>
              <RotateCcw />
              Go back and retry
            </Link>
          </Button>
        ) : (
          <Button asChild>
            <Link href="/">
              <ArrowLeft />
              Back to checkout
            </Link>
          </Button>
        )}
        <Button asChild variant="outline">
          <Link href="/transactions">
            <List />
            View all transactions
          </Link>
        </Button>
        <Button variant="ghost" className="sm:ml-auto" onClick={() => window.print()}>
          <Printer />
          Print
        </Button>
      </CardFooter>
    </Card>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function ErrorLine({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return (
    <p className="text-sm text-red-600 dark:text-red-400">
      {error.title}
      {error.detail ? `: ${error.detail}` : ""}
    </p>
  );
}
