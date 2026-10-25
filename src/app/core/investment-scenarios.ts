import { calculateMonthlyEmi, calculateOutstandingLoanBalance } from './investment-calculator';
import { calculateIrr } from './irr';

export type ScenarioName = 'conservative' | 'base' | 'optimistic';

export interface ScenarioAssumptionRates {
  propertyAppreciationPercent: number;
  rentGrowthPercent: number;
  vacancyPercent: number;
  operatingExpenseGrowthPercent: number;
  sellingCostPercent: number;
}

/**
 * Illustrative modelling defaults only — not Bengaluru market forecasts.
 */
export const SCENARIO_ASSUMPTIONS: Record<ScenarioName, ScenarioAssumptionRates> = {
  conservative: {
    propertyAppreciationPercent: 2,
    rentGrowthPercent: 2,
    vacancyPercent: 8,
    operatingExpenseGrowthPercent: 6,
    sellingCostPercent: 2,
  },
  base: {
    propertyAppreciationPercent: 5,
    rentGrowthPercent: 4,
    vacancyPercent: 5,
    operatingExpenseGrowthPercent: 5,
    sellingCostPercent: 2,
  },
  optimistic: {
    propertyAppreciationPercent: 8,
    rentGrowthPercent: 6,
    vacancyPercent: 3,
    operatingExpenseGrowthPercent: 4,
    sellingCostPercent: 2,
  },
};

export const SCENARIO_ORDER: ScenarioName[] = ['conservative', 'base', 'optimistic'];

export interface ScenarioPropertyInput {
  purchasePrice: number;
  monthlyRent: number;
  monthlyMaintenance: number;
  downPaymentPercent: number;
  annualInterestRatePercent: number;
  loanTenureYears: number;
  acquisitionCostPercent: number;
  annualPropertyTax: number;
  annualRepairAndMaintenance: number;
  holdingPeriodYears: number;
}

export interface ScenarioYearSnapshot {
  year: number;
  propertyValue: number;
  potentialRent: number;
  effectiveRent: number;
  operatingExpenses: number;
  annualLoanPayments: number;
  /**
   * Operating cash flow after opex and actual loan payments.
   * Year 0 is 0: acquisition is captured as the initial equity outflow, not as rent.
   */
  cashFlowAfterDebtService: number;
  outstandingLoan: number;
  netSaleProceeds: number;
}

export interface ScenarioResult {
  name: ScenarioName;
  assumptions: ScenarioAssumptionRates;
  /**
   * Timing: purchase at t = 0. Operating cash flows and the exit sale are
   * end-of-year amounts for years 1…H. Year H includes that year's operations
   * plus net sale proceeds. Loan payments stop after the contractual tenure.
   */
  initialCashInvested: number;
  equityCashFlows: number[];
  cumulativeRentalCashFlowAfterDebtService: number;
  exitPropertyValue: number;
  outstandingLoanAtExit: number;
  sellingCosts: number;
  netSaleProceeds: number;
  totalCashReceived: number;
  netProfit: number;
  grossPropertyAppreciationPercent: number;
  annualisedEquityIrr: number | null;
  irrUnavailableReason: string | null;
  years: ScenarioYearSnapshot[];
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

function roundToPaise(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function inClosedRange(value: unknown, min: number, max: number): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed >= min && parsed <= max;
}

function isNonNegative(value: unknown): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed >= 0;
}

function isPositive(value: unknown): value is number {
  const parsed = toFiniteNumber(value);
  return parsed !== null && parsed > 0;
}

function isWholeYear(value: unknown): value is number {
  const parsed = toFiniteNumber(value);
  if (parsed === null || parsed < 0) {
    return false;
  }
  return Math.abs(parsed - Math.round(parsed)) < 1e-9;
}

export function isValidScenarioRates(
  rates: Partial<ScenarioAssumptionRates> | null | undefined,
): rates is ScenarioAssumptionRates {
  if (!rates) {
    return false;
  }
  return (
    isNonNegative(rates.propertyAppreciationPercent) &&
    isNonNegative(rates.rentGrowthPercent) &&
    inClosedRange(rates.vacancyPercent, 0, 100) &&
    isNonNegative(rates.operatingExpenseGrowthPercent) &&
    inClosedRange(rates.sellingCostPercent, 0, 100)
  );
}

export function isValidScenarioPropertyInput(
  input: Partial<ScenarioPropertyInput> | null | undefined,
): input is ScenarioPropertyInput {
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
    isWholeYear(input.holdingPeriodYears)
  );
}

function grown(base: number, ratePercent: number, years: number): number {
  return base * Math.pow(1 + ratePercent / 100, years);
}

function loanPaymentsForYear(
  year: number,
  monthlyEmi: number,
  loanTenureYears: number,
): number {
  if (year <= 0 || monthlyEmi <= 0) {
    return 0;
  }
  const tenureMonths = loanTenureYears * 12;
  const startMonth = (year - 1) * 12;
  if (startMonth >= tenureMonths - 1e-12) {
    return 0;
  }
  const monthsThisYear = Math.min(12, tenureMonths - startMonth);
  return monthsThisYear > 0 ? monthlyEmi * monthsThisYear : 0;
}

export function calculateScenario(
  property: Partial<ScenarioPropertyInput> | null | undefined,
  rates: Partial<ScenarioAssumptionRates> | null | undefined,
  name: ScenarioName,
): ScenarioResult | null {
  if (!isValidScenarioPropertyInput(property) || !isValidScenarioRates(rates)) {
    return null;
  }

  const purchasePrice = toFiniteNumber(property.purchasePrice) as number;
  const monthlyRent = toFiniteNumber(property.monthlyRent) as number;
  const monthlyMaintenance = toFiniteNumber(property.monthlyMaintenance) as number;
  const downPaymentPercent = toFiniteNumber(property.downPaymentPercent) as number;
  const annualInterestRatePercent = toFiniteNumber(property.annualInterestRatePercent) as number;
  const loanTenureYears = toFiniteNumber(property.loanTenureYears) as number;
  const acquisitionCostPercent = toFiniteNumber(property.acquisitionCostPercent) as number;
  const annualPropertyTax = toFiniteNumber(property.annualPropertyTax) as number;
  const annualRepairAndMaintenance = toFiniteNumber(property.annualRepairAndMaintenance) as number;
  const holdingPeriodYears = Math.round(toFiniteNumber(property.holdingPeriodYears) as number);

  const downPaymentAmount = roundToPaise(purchasePrice * (downPaymentPercent / 100));
  const loanPrincipal = Math.max(0, roundToPaise(purchasePrice - downPaymentAmount));
  const acquisitionCostAmount = roundToPaise(purchasePrice * (acquisitionCostPercent / 100));
  const initialCashInvested = roundToPaise(downPaymentAmount + acquisitionCostAmount);
  const monthlyEmi = calculateMonthlyEmi(loanPrincipal, annualInterestRatePercent, loanTenureYears);
  const startingPotentialRent = monthlyRent * 12;
  const startingOperatingExpenses =
    monthlyMaintenance * 12 + annualPropertyTax + annualRepairAndMaintenance;

  const assumptions: ScenarioAssumptionRates = {
    propertyAppreciationPercent: toFiniteNumber(rates.propertyAppreciationPercent) as number,
    rentGrowthPercent: toFiniteNumber(rates.rentGrowthPercent) as number,
    vacancyPercent: toFiniteNumber(rates.vacancyPercent) as number,
    operatingExpenseGrowthPercent: toFiniteNumber(rates.operatingExpenseGrowthPercent) as number,
    sellingCostPercent: toFiniteNumber(rates.sellingCostPercent) as number,
  };

  const years: ScenarioYearSnapshot[] = [];
  const equityCashFlows: number[] = [];
  let cumulativeRentalCashFlowAfterDebtService = 0;

  for (let year = 0; year <= holdingPeriodYears; year += 1) {
    const propertyValue = roundToPaise(
      grown(purchasePrice, assumptions.propertyAppreciationPercent, year),
    );
    const potentialRent = roundToPaise(grown(startingPotentialRent, assumptions.rentGrowthPercent, year));
    const effectiveRent = roundToPaise(potentialRent * (1 - assumptions.vacancyPercent / 100));
    const operatingExpenses = roundToPaise(
      grown(startingOperatingExpenses, assumptions.operatingExpenseGrowthPercent, year),
    );
    const outstandingLoan = roundToPaise(
      calculateOutstandingLoanBalance(
        loanPrincipal,
        annualInterestRatePercent,
        loanTenureYears,
        year,
      ),
    );
    const annualLoanPayments =
      year === 0
        ? 0
        : roundToPaise(loanPaymentsForYear(year, monthlyEmi, loanTenureYears));
    const operatingCashFlow =
      year === 0 ? 0 : roundToPaise(effectiveRent - operatingExpenses - annualLoanPayments);

    if (year >= 1) {
      cumulativeRentalCashFlowAfterDebtService = roundToPaise(
        cumulativeRentalCashFlowAfterDebtService + operatingCashFlow,
      );
    }

    years.push({
      year,
      propertyValue,
      potentialRent,
      effectiveRent,
      operatingExpenses,
      annualLoanPayments,
      cashFlowAfterDebtService: operatingCashFlow,
      outstandingLoan,
      netSaleProceeds: 0,
    });
    equityCashFlows.push(year === 0 ? roundToPaise(-initialCashInvested) : operatingCashFlow);
  }

  const exit = years[holdingPeriodYears];
  const sellingCosts = roundToPaise(exit.propertyValue * (assumptions.sellingCostPercent / 100));
  const netSaleProceeds = roundToPaise(exit.propertyValue - sellingCosts - exit.outstandingLoan);
  exit.netSaleProceeds = netSaleProceeds;
  equityCashFlows[holdingPeriodYears] = roundToPaise(
    equityCashFlows[holdingPeriodYears] + netSaleProceeds,
  );

  const totalCashReceived = roundToPaise(cumulativeRentalCashFlowAfterDebtService + netSaleProceeds);
  const netProfit = roundToPaise(totalCashReceived - initialCashInvested);
  const grossPropertyAppreciationPercent =
    purchasePrice === 0 ? 0 : ((exit.propertyValue / purchasePrice) - 1) * 100;

  let annualisedEquityIrr: number | null = null;
  let irrUnavailableReason: string | null = null;
  if (holdingPeriodYears === 0) {
    irrUnavailableReason =
      'Holding period is zero, so there is no elapsed time over which to annualise a return.';
  } else {
    const irr = calculateIrr(equityCashFlows);
    annualisedEquityIrr = irr.rate;
    irrUnavailableReason = irr.reason;
  }

  return {
    name,
    assumptions,
    initialCashInvested,
    equityCashFlows,
    cumulativeRentalCashFlowAfterDebtService,
    exitPropertyValue: exit.propertyValue,
    outstandingLoanAtExit: exit.outstandingLoan,
    sellingCosts,
    netSaleProceeds,
    totalCashReceived,
    netProfit,
    grossPropertyAppreciationPercent,
    annualisedEquityIrr,
    irrUnavailableReason,
    years,
  };
}

export function calculateAllScenarios(
  property: Partial<ScenarioPropertyInput> | null | undefined,
  ratesByScenario: Partial<Record<ScenarioName, Partial<ScenarioAssumptionRates>>> | null | undefined,
): ScenarioResult[] | null {
  if (!isValidScenarioPropertyInput(property) || !ratesByScenario) {
    return null;
  }

  const results: ScenarioResult[] = [];
  for (const name of SCENARIO_ORDER) {
    const result = calculateScenario(property, ratesByScenario[name], name);
    if (!result) {
      return null;
    }
    results.push(result);
  }
  return results;
}
