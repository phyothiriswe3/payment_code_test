import { Suspense } from "react";
import ResultView from "@/components/ResultView";
import { Skeleton } from "@/components/ui/skeleton";

type Props = PageProps<"/result/[invoiceNo]">;

async function Result({ params }: Pick<Props, "params">) {
  const { invoiceNo } = await params;
  return <ResultView invoiceNo={decodeURIComponent(invoiceNo)} />;
}

export default function ResultPage({ params }: Props) {
  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8 md:py-12 print:max-w-none print:p-0">
      <Suspense fallback={<Skeleton className="h-96 w-full" />}>
        <Result params={params} />
      </Suspense>
    </div>
  );
}
