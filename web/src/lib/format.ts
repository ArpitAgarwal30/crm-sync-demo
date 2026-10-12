const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function formatDateTime(iso: string): string {
  return dateTime.format(new Date(iso));
}

/** Matches the engine's FieldChange.Describe: null shows as "(empty)". */
export function showValue(value: string | null): string {
  return value === null ? "(empty)" : value;
}
