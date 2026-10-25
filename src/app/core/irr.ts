/**
 * Annual IRR for equally spaced cash flows CF_0 ... CF_n.
 * Solves NPV(r) = Σ CF_t / (1+r)^t = 0 with Newton–Raphson and bisection.
 * Returns null when no meaningful finite rate exists.
 */
export interface IrrResult {
  rate: number | null;
  reason: string | null;
}

function npv(rate: number, cashFlows: number[]): number {
  let total = 0;
  for (let t = 0; t < cashFlows.length; t += 1) {
    total += cashFlows[t] / Math.pow(1 + rate, t);
  }
  return total;
}

function npvDerivative(rate: number, cashFlows: number[]): number {
  let total = 0;
  for (let t = 1; t < cashFlows.length; t += 1) {
    total -= (t * cashFlows[t]) / Math.pow(1 + rate, t + 1);
  }
  return total;
}

function hasSignChange(cashFlows: number[]): boolean {
  let previous = 0;
  for (const cashFlow of cashFlows) {
    if (cashFlow === 0 || !Number.isFinite(cashFlow)) {
      continue;
    }
    const sign = cashFlow > 0 ? 1 : -1;
    if (previous !== 0 && sign !== previous) {
      return true;
    }
    previous = sign;
  }
  return false;
}

function newtonIrr(cashFlows: number[]): number | null {
  let rate = 0.1;
  for (let i = 0; i < 80; i += 1) {
    if (rate <= -0.999999) {
      return null;
    }
    const value = npv(rate, cashFlows);
    const slope = npvDerivative(rate, cashFlows);
    if (!Number.isFinite(value) || !Number.isFinite(slope) || Math.abs(slope) < 1e-14) {
      return null;
    }
    const next = rate - value / slope;
    if (!Number.isFinite(next) || next <= -0.999999 || next > 20) {
      return null;
    }
    if (Math.abs(next - rate) < 1e-12) {
      return Math.abs(npv(next, cashFlows)) < 1e-6 ? next : null;
    }
    rate = next;
  }
  return Math.abs(npv(rate, cashFlows)) < 1e-6 ? rate : null;
}

function bisectionIrr(cashFlows: number[]): number | null {
  let low = -0.99;
  let high = 10;
  let npvLow = npv(low, cashFlows);
  let npvHigh = npv(high, cashFlows);

  if (!Number.isFinite(npvLow) || !Number.isFinite(npvHigh)) {
    return null;
  }

  if (npvLow * npvHigh > 0) {
    high = 50;
    npvHigh = npv(high, cashFlows);
    if (!Number.isFinite(npvHigh) || npvLow * npvHigh > 0) {
      return null;
    }
  }

  for (let i = 0; i < 120; i += 1) {
    const mid = (low + high) / 2;
    const npvMid = npv(mid, cashFlows);
    if (!Number.isFinite(npvMid)) {
      return null;
    }
    if (Math.abs(npvMid) < 1e-8 || Math.abs(high - low) < 1e-12) {
      return mid;
    }
    if (npvLow * npvMid <= 0) {
      high = mid;
      npvHigh = npvMid;
    } else {
      low = mid;
      npvLow = npvMid;
    }
  }

  const rate = (low + high) / 2;
  return Math.abs(npv(rate, cashFlows)) < 1e-4 ? rate : null;
}

export function calculateIrr(cashFlows: number[] | null | undefined): IrrResult {
  if (!cashFlows || cashFlows.length < 2) {
    return {
      rate: null,
      reason: 'An annualised equity IRR needs at least two dated cash flows.',
    };
  }

  if (cashFlows.some((cashFlow) => !Number.isFinite(cashFlow))) {
    return {
      rate: null,
      reason: 'Cash flows contain a non-finite value, so IRR is not available.',
    };
  }

  if (cashFlows.every((cashFlow) => cashFlow === 0)) {
    return {
      rate: null,
      reason: 'All cash flows are zero, so IRR is not defined.',
    };
  }

  if (!hasSignChange(cashFlows)) {
    return {
      rate: null,
      reason: 'Cash flows do not change sign, so a meaningful equity IRR is not defined.',
    };
  }

  const rate = newtonIrr(cashFlows) ?? bisectionIrr(cashFlows);
  if (rate === null || !Number.isFinite(rate) || rate <= -1) {
    return {
      rate: null,
      reason: 'No finite IRR was found for this cash-flow series.',
    };
  }

  return { rate, reason: null };
}
