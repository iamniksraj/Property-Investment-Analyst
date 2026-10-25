const inrFormatter = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 0,
  minimumFractionDigits: 0,
});

const inrPreciseFormatter = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 2,
  minimumFractionDigits: 2,
});

const percentFormatter = new Intl.NumberFormat('en-IN', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function formatInr(value: number, fractionDigits: 0 | 2 = 0): string {
  if (!Number.isFinite(value)) {
    return '—';
  }

  return fractionDigits === 2 ? inrPreciseFormatter.format(value) : inrFormatter.format(value);
}

export function formatPercent(value: number): string {
  if (!Number.isFinite(value)) {
    return '—';
  }

  return `${percentFormatter.format(value)}%`;
}

export function formatIrr(rate: number | null | undefined): string {
  if (rate === null || rate === undefined || !Number.isFinite(rate)) {
    return 'Not available';
  }

  return formatPercent(rate * 100);
}

/** Compact Indian units for comparison tables. Does not replace grouped INR amounts. */
export function formatInrCompact(value: number): string {
  if (!Number.isFinite(value)) {
    return '—';
  }

  const sign = value < 0 ? '-' : '';
  const absolute = Math.abs(value);

  if (absolute >= 1_00_00_000) {
    return `${sign}₹${percentFormatter.format(absolute / 1_00_00_000)} crore`;
  }

  if (absolute >= 1_00_000) {
    return `${sign}₹${percentFormatter.format(absolute / 1_00_000)} lakh`;
  }

  return formatInr(value);
}
