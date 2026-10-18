import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { TransactionStatus } from "@/lib/types";

const styles: Record<TransactionStatus, string> = {
  Succeeded:
    "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300",
  Failed: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300",
  Pending: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300",
};

export function StatusBadge({
  status,
  className,
}: {
  status: TransactionStatus;
  className?: string;
}) {
  return (
    <Badge
      variant="secondary"
      className={cn("border-transparent", styles[status], className)}
    >
      {status}
    </Badge>
  );
}
