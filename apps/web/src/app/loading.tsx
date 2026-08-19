import { Skeleton } from '@/components/ui/skeleton';

const SKELETON_KEYS = ['card-1', 'card-2', 'card-3', 'card-4'] as const;

export default function Loading() {
  return (
    <div className="container mx-auto px-4 py-12 flex flex-col gap-6 max-w-5xl">
      <div className="flex flex-col gap-2">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {SKELETON_KEYS.map((key) => (
          <div key={key} className="rounded-xl border bg-card p-4 flex flex-col gap-3">
            <Skeleton className="h-4 w-28" />
            <Skeleton className="h-7 w-20" />
            <Skeleton className="h-3 w-40 max-w-full" />
          </div>
        ))}
      </div>

      <div className="rounded-xl border bg-card p-4 flex flex-col gap-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    </div>
  );
}
