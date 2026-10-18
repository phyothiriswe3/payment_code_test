import { Suspense } from "react";
import TransactionsTable from "@/components/TransactionsTable";
import { Skeleton } from "@/components/ui/skeleton";

export default function TransactionsPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-8 md:px-6">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold tracking-tight">Transactions</h1>
        <p className="text-sm text-muted-foreground">
          Every checkout attempt and its latest payment status.
        </p>
      </div>
      {/* useSearchParams in the table needs a Suspense boundary. */}
      <Suspense fallback={<Skeleton className="h-96 w-full" />}>
        <TransactionsTable />
      </Suspense>
    </div>
  );
}
