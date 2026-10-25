import {
  calculateInvestmentMetrics,
  calculateMortgageCashFlow,
  MORTGAGE_ASSUMPTIONS,
} from './investment-calculator';
import {
  calculateAllScenarios,
  SCENARIO_ASSUMPTIONS,
  ScenarioAssumptionRates,
  ScenarioName,
  ScenarioResult,
} from './investment-scenarios';
import {
  ComparisonHighlights,
  ComparisonPropertyDraft,
  ComparisonRow,
  ComparisonView,
  PropertyAssessment,
  SignAssessment,
} from './comparison-models';

export function cloneScenarioAssumptions(): Record<ScenarioName, ScenarioAssumptionRates> {
  return {
    conservative: { ...SCENARIO_ASSUMPTIONS.conservative },
    base: { ...SCENARIO_ASSUMPTIONS.base },
    optimistic: { ...SCENARIO_ASSUMPTIONS.optimistic },
  };
}

export function createEmptyPropertyDraft(id: string): ComparisonPropertyDraft {
  return {
    id,
    name: '',
    locality: '',
    builderProject: '',
    propertyType: 'Apartment',
    purchasePrice: null,
    superBuiltUpAreaSqFt: null,
    monthlyRent: null,
    monthlyMaintenance: null,
    downPaymentPercent: MORTGAGE_ASSUMPTIONS.downPaymentPercent,
    annualInterestRatePercent: MORTGAGE_ASSUMPTIONS.annualInterestRatePercent,
    loanTenureYears: MORTGAGE_ASSUMPTIONS.loanTenureYears,
    acquisitionCostPercent: MORTGAGE_ASSUMPTIONS.acquisitionCostPercent,
    annualPropertyTax: MORTGAGE_ASSUMPTIONS.annualPropertyTax,
    annualRepairAndMaintenance: MORTGAGE_ASSUMPTIONS.annualRepairAndMaintenance,
    vacancyPercent: MORTGAGE_ASSUMPTIONS.vacancyPercent,
    holdingPeriodYears: MORTGAGE_ASSUMPTIONS.holdingPeriodYears,
    scenarios: cloneScenarioAssumptions(),
  };
}

function signOf(value: number): SignAssessment {
  if (value > 0) {
    return 'positive';
  }
  if (value < 0) {
    return 'negative';
  }
  return 'zero';
}

function scenarioByName(scenarios: ScenarioResult[] | null, name: ScenarioName): ScenarioResult | null {
  return scenarios?.find((item) => item.name === name) ?? null;
}

export function assessProperty(row: Pick<ComparisonRow, 'mortgage' | 'base' | 'conservative'>): PropertyAssessment | null {
  if (!row.mortgage || !row.base) {
    return null;
  }

  const annualCashFlow = signOf(row.mortgage.annualCashFlowAfterOperatingExpensesAndLoanPayments);
  const baseProfit = signOf(row.base.netProfit);
  const baseIrr: PropertyAssessment['baseIrr'] =
    row.base.annualisedEquityIrr === null ? 'unavailable' : signOf(row.base.annualisedEquityIrr);

  let conservativeSensitivity: PropertyAssessment['conservativeSensitivity'] = 'unavailable';
  if (row.conservative) {
    const gap = row.base.netProfit - row.conservative.netProfit;
    if (Math.abs(gap) < 1_000) {
      conservativeSensitivity = 'similar';
    } else if (gap > 0) {
      conservativeSensitivity = 'more-sensitive';
    } else {
      conservativeSensitivity = 'more-resilient';
    }
  }

  const notes: string[] = [];
  notes.push(
    annualCashFlow === 'positive'
      ? 'Year-one cash flow after debt service is positive under the constant-rent snapshot.'
      : annualCashFlow === 'negative'
        ? 'Year-one cash flow after debt service is negative under the constant-rent snapshot.'
        : 'Year-one cash flow after debt service is zero under the constant-rent snapshot.',
  );
  notes.push(
    baseProfit === 'positive'
      ? 'Base-case net profit is positive.'
      : baseProfit === 'negative'
        ? 'Base-case net profit is negative.'
        : 'Base-case net profit is zero.',
  );
  if (baseIrr === 'unavailable') {
    notes.push(
      row.base.irrUnavailableReason ??
        'Base-case annualised equity IRR is not available for this cash-flow series.',
    );
  } else if (baseIrr === 'positive') {
    notes.push('Base-case annualised equity IRR is positive.');
  } else if (baseIrr === 'negative') {
    notes.push('Base-case annualised equity IRR is negative.');
  } else {
    notes.push('Base-case annualised equity IRR is zero.');
  }
  if (conservativeSensitivity === 'more-sensitive') {
    notes.push('Conservative-case net profit is lower than the Base case, so the outcome is sensitive to weaker assumptions.');
  } else if (conservativeSensitivity === 'more-resilient') {
    notes.push('Conservative-case net profit is at least as high as the Base case.');
  }

  return {
    annualCashFlow,
    baseProfit,
    baseIrr,
    conservativeSensitivity,
    notes,
  };
}

export function buildComparisonRow(draft: ComparisonPropertyDraft): ComparisonRow {
  const snapshot = calculateInvestmentMetrics({
    purchasePrice: draft.purchasePrice ?? Number.NaN,
    superBuiltUpAreaSqFt: draft.superBuiltUpAreaSqFt ?? Number.NaN,
    monthlyRent: draft.monthlyRent ?? Number.NaN,
    monthlyMaintenance: draft.monthlyMaintenance ?? Number.NaN,
  });
  const mortgage = calculateMortgageCashFlow({
    purchasePrice: draft.purchasePrice ?? Number.NaN,
    monthlyRent: draft.monthlyRent ?? Number.NaN,
    monthlyMaintenance: draft.monthlyMaintenance ?? Number.NaN,
    downPaymentPercent: draft.downPaymentPercent,
    annualInterestRatePercent: draft.annualInterestRatePercent,
    loanTenureYears: draft.loanTenureYears,
    acquisitionCostPercent: draft.acquisitionCostPercent,
    annualPropertyTax: draft.annualPropertyTax,
    annualRepairAndMaintenance: draft.annualRepairAndMaintenance,
    vacancyPercent: draft.vacancyPercent,
    holdingPeriodYears: draft.holdingPeriodYears,
  });
  const scenarios = calculateAllScenarios(
    {
      purchasePrice: draft.purchasePrice ?? Number.NaN,
      monthlyRent: draft.monthlyRent ?? Number.NaN,
      monthlyMaintenance: draft.monthlyMaintenance ?? Number.NaN,
      downPaymentPercent: draft.downPaymentPercent,
      annualInterestRatePercent: draft.annualInterestRatePercent,
      loanTenureYears: draft.loanTenureYears,
      acquisitionCostPercent: draft.acquisitionCostPercent,
      annualPropertyTax: draft.annualPropertyTax,
      annualRepairAndMaintenance: draft.annualRepairAndMaintenance,
      holdingPeriodYears: draft.holdingPeriodYears,
    },
    draft.scenarios,
  );
  const base = scenarioByName(scenarios, 'base');
  const conservative = scenarioByName(scenarios, 'conservative');
  const optimistic = scenarioByName(scenarios, 'optimistic');
  const ready = !!(snapshot && mortgage && scenarios && base);
  const partial: ComparisonRow = {
    id: draft.id,
    draft,
    ready,
    snapshot,
    mortgage,
    scenarios,
    base,
    conservative,
    optimistic,
    assessment: null,
  };
  partial.assessment = ready ? assessProperty(partial) : null;
  return partial;
}

function extremeIds(
  rows: ComparisonRow[],
  read: (row: ComparisonRow) => number | null,
  mode: 'min' | 'max',
): string[] {
  const scored = rows
    .filter((row) => row.ready)
    .map((row) => ({ id: row.id, value: read(row) }))
    .filter((item): item is { id: string; value: number } => item.value !== null && Number.isFinite(item.value));

  if (scored.length === 0) {
    return [];
  }

  const extreme = mode === 'max'
    ? Math.max(...scored.map((item) => item.value))
    : Math.min(...scored.map((item) => item.value));

  return scored.filter((item) => item.value === extreme).map((item) => item.id);
}

export function buildComparisonHighlights(rows: ComparisonRow[]): ComparisonHighlights {
  const ready = rows.filter((row) => row.ready);
  const negativeCashFlow = ready.filter(
    (row) => (row.mortgage?.annualCashFlowAfterOperatingExpensesAndLoanPayments ?? 0) < 0,
  );

  return {
    highestGrossYieldIds: extremeIds(
      ready,
      (row) => row.snapshot?.grossAnnualRentalYieldPercent ?? null,
      'max',
    ),
    lowestInitialCashIds: extremeIds(
      ready,
      (row) => row.mortgage?.totalInitialAcquisitionCash ?? null,
      'min',
    ),
    bestBaseIrrIds: extremeIds(ready, (row) => row.base?.annualisedEquityIrr ?? null, 'max'),
    strongestConservativeIds: extremeIds(
      ready,
      (row) => row.conservative?.netProfit ?? null,
      'max',
    ),
    highestBaseNetProfitIds: extremeIds(ready, (row) => row.base?.netProfit ?? null, 'max'),
    lowestCashFlowShortfallIds: extremeIds(
      negativeCashFlow,
      (row) => row.mortgage?.annualCashFlowAfterOperatingExpensesAndLoanPayments ?? null,
      'max',
    ),
  };
}

export function buildComparisonView(drafts: ComparisonPropertyDraft[]): ComparisonView {
  const rows = drafts.map((draft) => buildComparisonRow(draft));
  return {
    rows,
    highlights: buildComparisonHighlights(rows),
  };
}

export function createIllustrativeSampleProperties(): ComparisonPropertyDraft[] {
  const whitefield = createEmptyPropertyDraft('sample-whitefield');
  whitefield.name = 'Whitefield 2 BHK';
  whitefield.locality = 'Whitefield';
  whitefield.builderProject = 'Illustrative project A';
  whitefield.purchasePrice = 1_20_00_000;
  whitefield.superBuiltUpAreaSqFt = 1_200;
  whitefield.monthlyRent = 40_000;
  whitefield.monthlyMaintenance = 5_000;

  const hsr = createEmptyPropertyDraft('sample-hsr');
  hsr.name = 'HSR Layout 3 BHK';
  hsr.locality = 'HSR Layout';
  hsr.builderProject = 'Illustrative project B';
  hsr.purchasePrice = 1_80_00_000;
  hsr.superBuiltUpAreaSqFt = 1_650;
  hsr.monthlyRent = 65_000;
  hsr.monthlyMaintenance = 7_000;
  hsr.downPaymentPercent = 25;

  return [whitefield, hsr];
}
