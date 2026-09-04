export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex flex-col gap-2">
      <h1 className="text-xl font-semibold">{title}</h1>
      <p className="text-[var(--color-text-muted)]">Coming in a later milestone.</p>
    </div>
  )
}
