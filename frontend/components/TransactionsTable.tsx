"use client";

import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table";
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Inbox,
  RefreshCw,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { StatusBadge } from "@/components/status-badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ApiError, listTransactions } from "@/lib/api";
import { formatDateTime, formatMoney, paymentTypeLabel } from "@/lib/format";
import type { PagedResult, Transaction, TransactionStatus } from "@/lib/types";

const PAGE_SIZES = [10, 20, 50] as const;
const DEFAULT_PAGE_SIZE = 10;
const STATUSES: TransactionStatus[] = ["Succeeded", "Pending", "Failed"];
const ALL = "all";
const EMPTY: Transaction[] = [];

const features = tableFeatures({});
const helper = createColumnHelper<typeof features, Transaction>();
const columns = helper.columns([
  helper.display({
    id: "invoiceNo",
    header: "Invoice No",
    cell: ({ row }) => (
      <Link
        href={`/result/${encodeURIComponent(row.original.invoiceNo)}`}
        className="font-mono text-xs font-medium underline-offset-4 hover:underline"
      >
        {row.original.invoiceNo}
      </Link>
    ),
  }),
  helper.display({
    id: "status",
    header: "Status",
    cell: ({ row }) => <StatusBadge status={row.original.status} />,
  }),
  helper.display({
    id: "createdAt",
    header: "Date",
    cell: ({ row }) => (
      <span className="whitespace-nowrap">{formatDateTime(row.original.createdAt)}</span>
    ),
  }),
  helper.display({
    id: "customer",
    header: "Customer",
    cell: ({ row }) => (
      <div className="flex flex-col">
        <span className="font-medium">{row.original.customerName}</span>
        <span className="text-xs text-muted-foreground">{row.original.customerEmail}</span>
      </div>
    ),
  }),
  helper.display({
    id: "product",
    header: "Product",
    cell: ({ row }) => (
      <div className="flex flex-col">
        <span>{row.original.productName}</span>
        <span className="text-xs text-muted-foreground">
          {paymentTypeLabel(row.original.paymentType)}
        </span>
      </div>
    ),
  }),
  helper.display({
    id: "amount",
    header: () => <div className="text-right">Amount</div>,
    cell: ({ row }) => (
      <div className="text-right font-medium tabular-nums whitespace-nowrap">
        {formatMoney(row.original.amount, row.original.currency)}
      </div>
    ),
  }),
  helper.display({
    id: "method",
    header: "Method",
    cell: ({ row }) => {
      const { paymentChannel, maskedCardNo } = row.original;
      if (!paymentChannel && !maskedCardNo) return <span className="text-muted-foreground">—</span>;
      return (
        <div className="flex flex-col">
          <span>{paymentChannel ?? "—"}</span>
          {maskedCardNo && (
            <span className="font-mono text-xs text-muted-foreground">{maskedCardNo}</span>
          )}
        </div>
      );
    },
  }),
]);

type Loaded =
  | { key: string; data: PagedResult<Transaction>; error?: undefined }
  | { key: string; data?: undefined; error: ApiError };

export default function TransactionsTable() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const page = Math.max(1, Math.floor(Number(searchParams.get("page"))) || 1);
  const sizeParam = Number(searchParams.get("pageSize"));
  const pageSize = PAGE_SIZES.find((s) => s === sizeParam) ?? DEFAULT_PAGE_SIZE;
  const status = STATUSES.find((s) => s === searchParams.get("status"));

  const [refreshCount, setRefreshCount] = useState(0);
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const requestKey = `${page}|${pageSize}|${status ?? ""}|${refreshCount}`;
  const loading = loaded?.key !== requestKey;

  useEffect(() => {
    let active = true;
    listTransactions({ page, pageSize, status })
      .then((data) => active && setLoaded({ key: requestKey, data }))
      .catch(
        (e) =>
          active &&
          setLoaded({
            key: requestKey,
            error: e instanceof ApiError ? e : new ApiError(0, "Unexpected error", String(e)),
          }),
      );
    return () => {
      active = false;
    };
  }, [page, pageSize, status, requestKey]);

  const result = loading ? undefined : loaded?.data;
  const error = loading ? undefined : loaded?.error;
  const table = useTable({ features, columns, data: result?.items ?? EMPTY });

  /** Writes the table state to the URL so the current view is shareable. */
  function navigate(next: { page?: number; pageSize?: number; status?: TransactionStatus | null }) {
    const qs = new URLSearchParams();
    const nextPage = next.page ?? page;
    const nextSize = next.pageSize ?? pageSize;
    const nextStatus = next.status === undefined ? status : next.status;
    if (nextPage > 1) qs.set("page", String(nextPage));
    if (nextSize !== DEFAULT_PAGE_SIZE) qs.set("pageSize", String(nextSize));
    if (nextStatus) qs.set("status", nextStatus);
    const query = qs.toString();
    router.push(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  const totalPages = result?.totalPages ?? 0;
  const canPrev = !loading && page > 1;
  const canNext = !loading && page < totalPages;

  return (
    <Card className="gap-0 py-0">
      <div className="flex flex-wrap items-center gap-2 border-b p-4">
        <Select
          value={status ?? ALL}
          onValueChange={(v) =>
            navigate({ page: 1, status: v === ALL ? null : (v as TransactionStatus) })
          }
        >
          <SelectTrigger className="w-40" aria-label="Filter by status">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>All statuses</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="ml-auto flex items-center gap-3">
          <span className="text-sm text-muted-foreground" aria-live="polite">
            {result ? `${result.totalCount} transaction${result.totalCount === 1 ? "" : "s"}` : ""}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setRefreshCount((n) => n + 1)}
            disabled={loading}
          >
            <RefreshCw className={loading ? "animate-spin" : undefined} />
            Refresh
          </Button>
        </div>
      </div>

      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((group) => (
            <TableRow key={group.id}>
              {group.headers.map((header) => (
                <TableHead key={header.id} className="px-4">
                  {header.isPlaceholder ? null : <table.FlexRender header={header} />}
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {loading ? (
            Array.from({ length: Math.min(pageSize, 10) }, (_, i) => (
              <TableRow key={`skeleton-${i}`}>
                {columns.map((_, j) => (
                  <TableCell key={j} className="px-4 py-3">
                    <Skeleton className="h-5 w-full min-w-16" />
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : error ? (
            <TableRow>
              <TableCell colSpan={columns.length} className="h-40 text-center">
                <div className="flex flex-col items-center gap-2 text-sm">
                  <AlertCircle className="size-6 text-destructive" />
                  <p className="font-medium">Could not load transactions</p>
                  <p className="text-muted-foreground">
                    {error.title}
                    {error.detail ? `: ${error.detail}` : ""}
                  </p>
                  <Button variant="outline" size="sm" onClick={() => setRefreshCount((n) => n + 1)}>
                    Try again
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          ) : table.getRowModel().rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={columns.length} className="h-40 text-center">
                <div className="flex flex-col items-center gap-2 text-sm">
                  <Inbox className="size-6 text-muted-foreground" />
                  <p className="font-medium">No transactions found</p>
                  <p className="text-muted-foreground">
                    {page > 1 && totalPages > 0
                      ? `This view only has ${totalPages} page${totalPages === 1 ? "" : "s"}.`
                      : status
                        ? `There are no ${status.toLowerCase()} transactions yet.`
                        : "Completed checkouts will appear here."}
                  </p>
                  {page > 1 && (
                    <Button variant="outline" size="sm" onClick={() => navigate({ page: 1 })}>
                      Go to first page
                    </Button>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ) : (
            table.getRowModel().rows.map((row) => (
              <TableRow key={row.id}>
                {row.getAllCells().map((cell) => (
                  <TableCell key={cell.id} className="px-4 py-3">
                    <table.FlexRender cell={cell} />
                  </TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>

      <div className="flex flex-col-reverse items-center justify-between gap-3 border-t p-4 sm:flex-row">
        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">Rows per page</span>
          <Select
            value={String(pageSize)}
            onValueChange={(v) => navigate({ page: 1, pageSize: Number(v) })}
          >
            <SelectTrigger size="sm" className="w-20" aria-label="Rows per page">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZES.map((s) => (
                <SelectItem key={s} value={String(s)}>
                  {s}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm font-medium tabular-nums">
            Page {page} of {Math.max(totalPages, 1)}
          </span>
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="icon-sm"
              aria-label="First page"
              disabled={!canPrev}
              onClick={() => navigate({ page: 1 })}
            >
              <ChevronsLeft />
            </Button>
            <Button
              variant="outline"
              size="icon-sm"
              aria-label="Previous page"
              disabled={!canPrev}
              onClick={() => navigate({ page: Math.min(page - 1, Math.max(totalPages, 1)) })}
            >
              <ChevronLeft />
            </Button>
            <Button
              variant="outline"
              size="icon-sm"
              aria-label="Next page"
              disabled={!canNext}
              onClick={() => navigate({ page: page + 1 })}
            >
              <ChevronRight />
            </Button>
            <Button
              variant="outline"
              size="icon-sm"
              aria-label="Last page"
              disabled={!canNext}
              onClick={() => navigate({ page: totalPages })}
            >
              <ChevronsRight />
            </Button>
          </div>
        </div>
      </div>
    </Card>
  );
}
