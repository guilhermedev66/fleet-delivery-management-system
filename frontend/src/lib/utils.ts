/**
 * Combine class name fragments, dropping falsy values.
 * Trivial now; swap for `clsx`/`tailwind-merge` if class conflicts
 * become common once real UI components land.
 */
export function cx(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ')
}
