export interface PropertyInvestmentInput {
  purchasePrice: number;
  superBuiltUpAreaSqFt: number;
  monthlyRent: number;
  monthlyMaintenance: number;
}

export interface PropertyInvestmentMetrics {
  pricePerSqFt: number;
  grossAnnualRentalYieldPercent: number;
  annualRentalIncome: number;
  estimatedAnnualMaintenance: number;
  netRentalIncomeBeforeOtherExpenses: number;
}

function toFiniteNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function isPositive(value: unknown): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed > 0;
}

function isNonNegative(value: unknown): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed >= 0;
}

export function isValidInvestmentInput(
  input: Partial<PropertyInvestmentInput> | null | undefined,
): input is PropertyInvestmentInput {
  if (!input) {
    return false;
  }

  return (
    isPositive(input.purchasePrice) &&
    isPositive(input.superBuiltUpAreaSqFt) &&
    isNonNegative(input.monthlyRent) &&
    isNonNegative(input.monthlyMaintenance)
  );
}

export function calculateInvestmentMetrics(
  input: Partial<PropertyInvestmentInput> | null | undefined,
): PropertyInvestmentMetrics | null {
  if (!isValidInvestmentInput(input)) {
    return null;
  }

  const purchasePrice = toFiniteNumber(input.purchasePrice) as number;
  const superBuiltUpAreaSqFt = toFiniteNumber(input.superBuiltUpAreaSqFt) as number;
  const monthlyRent = toFiniteNumber(input.monthlyRent) as number;
  const monthlyMaintenance = toFiniteNumber(input.monthlyMaintenance) as number;

  const annualRentalIncome = monthlyRent * 12;
  const estimatedAnnualMaintenance = monthlyMaintenance * 12;

  return {
    pricePerSqFt: purchasePrice / superBuiltUpAreaSqFt,
    annualRentalIncome,
    estimatedAnnualMaintenance,
    grossAnnualRentalYieldPercent: (annualRentalIncome / purchasePrice) * 100,
    netRentalIncomeBeforeOtherExpenses: annualRentalIncome - estimatedAnnualMaintenance,
  };
}

/**
 * Editable starter assumptions — not Bengaluru market quotes.
 * Rent, society maintenance, property tax and repairs are modelled as constant
 * annual amounts (monthly rent × 12, with no growth or inflation).
 */
export const MORTGAGE_ASSUMPTIONS = {
  downPaymentPercent: 20,
  annualInterestRatePercent: 8.5,
  loanTenureYears: 20,
  acquisitionCostPercent: 7,
  annualPropertyTax: 0,
  annualRepairAndMaintenance: 0,
  vacancyPercent: 5,
  holdingPeriodYears: 10,
} as const;

export interface MortgageCashFlowInput {
  purchasePrice: number;
  monthlyRent: number;
  monthlyMaintenance: number;
  downPaymentPercent: number;
  annualInterestRatePercent: number;
  loanTenureYears: number;
  acquisitionCostPercent: number;
  annualPropertyTax: number;
  annualRepairAndMaintenance: number;
  vacancyPercent: number;
  holdingPeriodYears: number;
}

export interface MortgageCashFlowMetrics {
  downPaymentAmount: number;
  loanPrincipal: number;
  monthlyEmi: number;
  /** Twelve EMIs in the year (standard amortisation, reducing-balance). */
  annualLoanPayments: number;
  acquisitionCostAmount: number;
  totalInitialAcquisitionCash: number;
  annualPotentialRent: number;
  annualRentAfterVacancy: number;
  /** Society maintenance + property tax + repairs. Vacancy is not an expense line. */
  annualOperatingExpenses: number;
  /**
   * Annual cash flow after operating expenses and after loan payments (debt service).
   * Not cash flow before financing.
   */
  annualCashFlowAfterOperatingExpensesAndLoanPayments: number;
  outstandingLoanBalanceAfterHoldingPeriod: number;
}

function inClosedRange(value: unknown, min: number, max: number): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed >= min && parsed <= max;
}

export function isValidMortgageCashFlowInput(
  input: Partial<MortgageCashFlowInput> | null | undefined,
): input is MortgageCashFlowInput {
  if (!input) {
    return false;
  }

  return (
    isPositive(input.purchasePrice) &&
    isNonNegative(input.monthlyRent) &&
    isNonNegative(input.monthlyMaintenance) &&
    inClosedRange(input.downPaymentPercent, 0, 100) &&
    isNonNegative(input.annualInterestRatePercent) &&
    isPositive(input.loanTenureYears) &&
    isNonNegative(input.acquisitionCostPercent) &&
    isNonNegative(input.annualPropertyTax) &&
    isNonNegative(input.annualRepairAndMaintenance) &&
    inClosedRange(input.vacancyPercent, 0, 100) &&
    isNonNegative(input.holdingPeriodYears)
  );
}

function monthlyInterestRate(annualInterestRatePercent: number): number {
  return annualInterestRatePercent / 100 / 12;
}

function roundToPaise(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

/**
 * Standard equated monthly instalment on a reducing-balance loan.
 * Zero interest → principal / number of months. Zero principal → 0.
 */
export function calculateMonthlyEmi(
  loanPrincipal: number,
  annualInterestRatePercent: number,
  loanTenureYears: number,
): number {
  if (!(loanPrincipal > 0) || !(loanTenureYears > 0) || annualInterestRatePercent < 0) {
    return 0;
  }

  const tenureMonths = loanTenureYears * 12;
  const monthlyRate = monthlyInterestRate(annualInterestRatePercent);

  if (monthlyRate === 0) {
    return loanPrincipal / tenureMonths;
  }

  const compound = Math.pow(1 + monthlyRate, tenureMonths);
  if (!Number.isFinite(compound) || compound === 1) {
    return 0;
  }

  const emi = (loanPrincipal * monthlyRate * compound) / (compound - 1);
  return Number.isFinite(emi) ? emi : 0;
}

/**
 * Remaining principal after holdingPeriodYears, using the same EMI schedule.
 * Holding periods at or beyond tenure (and any negative float) return 0.
 */
export function calculateOutstandingLoanBalance(
  loanPrincipal: number,
  annualInterestRatePercent: number,
  loanTenureYears: number,
  holdingPeriodYears: number,
): number {
  if (!(loanPrincipal > 0) || !(loanTenureYears > 0)) {
    return 0;
  }

  const tenureMonths = loanTenureYears * 12;
  const elapsedMonths = holdingPeriodYears * 12;

  if (elapsedMonths <= 0) {
    return loanPrincipal;
  }

  if (elapsedMonths >= tenureMonths) {
    return 0;
  }

  const monthlyRate = monthlyInterestRate(annualInterestRatePercent);
  let remaining: number;

  if (monthlyRate === 0) {
    remaining = loanPrincipal * (1 - elapsedMonths / tenureMonths);
  } else {
    const emi = calculateMonthlyEmi(loanPrincipal, annualInterestRatePercent, loanTenureYears);
    const growth = Math.pow(1 + monthlyRate, elapsedMonths);
    remaining = loanPrincipal * growth - (emi * (growth - 1)) / monthlyRate;
  }

  if (!Number.isFinite(remaining) || remaining < 0) {
    return 0;
  }

  return remaining < 1e-8 ? 0 : remaining;
}

export function calculateMortgageCashFlow(
  input: Partial<MortgageCashFlowInput> | null | undefined,
): MortgageCashFlowMetrics | null {
  if (!isValidMortgageCashFlowInput(input)) {
    return null;
  }

  const purchasePrice = toFiniteNumber(input.purchasePrice) as number;
  const monthlyRent = toFiniteNumber(input.monthlyRent) as number;
  const monthlyMaintenance = toFiniteNumber(input.monthlyMaintenance) as number;
  const downPaymentPercent = toFiniteNumber(input.downPaymentPercent) as number;
  const annualInterestRatePercent = toFiniteNumber(input.annualInterestRatePercent) as number;
  const loanTenureYears = toFiniteNumber(input.loanTenureYears) as number;
  const acquisitionCostPercent = toFiniteNumber(input.acquisitionCostPercent) as number;
  const annualPropertyTax = toFiniteNumber(input.annualPropertyTax) as number;
  const annualRepairAndMaintenance = toFiniteNumber(input.annualRepairAndMaintenance) as number;
  const vacancyPercent = toFiniteNumber(input.vacancyPercent) as number;
  const holdingPeriodYears = toFiniteNumber(input.holdingPeriodYears) as number;

  const downPaymentAmount = roundToPaise(purchasePrice * (downPaymentPercent / 100));
  const loanPrincipal = Math.max(0, roundToPaise(purchasePrice - downPaymentAmount));
  const monthlyEmi = roundToPaise(
    calculateMonthlyEmi(loanPrincipal, annualInterestRatePercent, loanTenureYears),
  );
  const annualLoanPayments = roundToPaise(monthlyEmi * 12);
  const acquisitionCostAmount = roundToPaise(purchasePrice * (acquisitionCostPercent / 100));
  const annualPotentialRent = roundToPaise(monthlyRent * 12);
  const annualRentAfterVacancy = roundToPaise(annualPotentialRent * (1 - vacancyPercent / 100));
  const annualOperatingExpenses = roundToPaise(
    monthlyMaintenance * 12 + annualPropertyTax + annualRepairAndMaintenance,
  );

  return {
    downPaymentAmount,
    loanPrincipal,
    monthlyEmi,
    annualLoanPayments,
    acquisitionCostAmount,
    totalInitialAcquisitionCash: roundToPaise(downPaymentAmount + acquisitionCostAmount),
    annualPotentialRent,
    annualRentAfterVacancy,
    annualOperatingExpenses,
    annualCashFlowAfterOperatingExpensesAndLoanPayments: roundToPaise(
      annualRentAfterVacancy - annualOperatingExpenses - annualLoanPayments,
    ),
    outstandingLoanBalanceAfterHoldingPeriod: roundToPaise(
      calculateOutstandingLoanBalance(
        loanPrincipal,
        annualInterestRatePercent,
        loanTenureYears,
        holdingPeriodYears,
      ),
    ),
  };
}
