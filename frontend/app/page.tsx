import { ShieldCheck } from "lucide-react";
import { Suspense } from "react";
import CheckoutForm from "@/components/CheckoutForm";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

async function Form({ searchParams }: Pick<PageProps<"/">, "searchParams">) {
  const { customerId, productId } = await searchParams;
  return (
    <CheckoutForm
      initialCustomerId={Number(customerId) || undefined}
      initialProductId={Number(productId) || undefined}
    />
  );
}

export default function CheckoutPage({ searchParams }: PageProps<"/">) {
  return (
    <div className="mx-auto w-full max-w-xl px-4 py-8 md:py-12">
      <Card>
        <CardHeader>
          <CardTitle className="text-xl">Checkout</CardTitle>
          <CardDescription className="flex items-center gap-1.5">
            <ShieldCheck className="size-4" />
            Secure payment powered by 2C2P.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Suspense fallback={<Skeleton className="h-64 w-full" />}>
            <Form searchParams={searchParams} />
          </Suspense>
        </CardContent>
      </Card>
    </div>
  );
}
