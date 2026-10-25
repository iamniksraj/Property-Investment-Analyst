import {
  buildComparisonRow,
  buildComparisonView,
  cloneScenarioAssumptions,
  createEmptyPropertyDraft,
  createIllustrativeSampleProperties,
} from './comparison-engine';
import { MAX_COMPARISON_PROPERTIES } from './comparison-models';

describe('comparison engine', () => {
  const readyDraft = () => {
    const draft = createEmptyPropertyDraft('p1');
    draft.name = 'One';
    draft.purchasePrice = 12_00_000;
    draft.superBuiltUpAreaSqFt = 1_200;
    draft.monthlyRent = 10_000;
    draft.monthlyMaintenance = 0;
    draft.downPaymentPercent = 100;
    draft.acquisitionCostPercent = 0;
    draft.vacancyPercent = 0;
    draft.holdingPeriodYears = 2;
    draft.scenarios = {
      conservative: {
        propertyAppreciationPercent: 0,
        rentGrowthPercent: 0,
        vacancyPercent: 0,
        operatingExpenseGrowthPercent: 0,
        sellingCostPercent: 0,
      },
      base: {
        propertyAppreciationPercent: 0,
        rentGrowthPercent: 0,
        vacancyPercent: 0,
        operatingExpenseGrowthPercent: 0,
        sellingCostPercent: 0,
      },
      optimistic: {
        propertyAppreciationPercent: 0,
        rentGrowthPercent: 0,
        vacancyPercent: 0,
        operatingExpenseGrowthPercent: 0,
        sellingCostPercent: 0,
      },
    };
    return draft;
  };

  it('leaves an incomplete draft unready and excluded from ranking', () => {
    const view = buildComparisonView([createEmptyPropertyDraft('blank')]);
    expect(view.rows).toHaveSize(1);
    expect(view.rows[0].ready).toBeFalse();
    expect(view.highlights.highestGrossYieldIds).toEqual([]);
  });

  it('compares one, two and three independent properties', () => {
    const first = readyDraft();
    const second = { ...readyDraft(), id: 'p2', name: 'Two', purchasePrice: 24_00_000, monthlyRent: 10_000 };
    const third = { ...readyDraft(), id: 'p3', name: 'Three', purchasePrice: 12_00_000, monthlyRent: 15_000 };
    third.scenarios = cloneScenarioAssumptions();
    third.scenarios.base = { ...first.scenarios.base };

    expect(buildComparisonView([first]).rows).toHaveSize(1);
    expect(buildComparisonView([first, second]).rows).toHaveSize(2);
    expect(buildComparisonView([first, second, third]).rows).toHaveSize(3);
    expect(MAX_COMPARISON_PROPERTIES).toBe(3);

    const two = buildComparisonView([first, second]);
    expect(two.rows[0].snapshot?.pricePerSqFt).toBe(1_000);
    expect(two.rows[0].snapshot?.grossAnnualRentalYieldPercent).toBe(10);
    expect(two.rows[1].snapshot?.pricePerSqFt).toBe(2_000);
    expect(two.rows[1].snapshot?.grossAnnualRentalYieldPercent).toBe(5);
    expect(two.highlights.highestGrossYieldIds).toEqual(['p1']);
    expect(two.highlights.lowestInitialCashIds).toEqual(['p1']);
  });

  it('keeps financing and scenario assumptions independent per property', () => {
    const cheap = readyDraft();
    cheap.downPaymentPercent = 100;
    const leveraged = { ...readyDraft(), id: 'p2', name: 'Levered' };
    leveraged.downPaymentPercent = 20;
    leveraged.acquisitionCostPercent = 0;
    leveraged.annualInterestRatePercent = 0;
    leveraged.loanTenureYears = 4;
    leveraged.holdingPeriodYears = 2;
    leveraged.scenarios = cloneScenarioAssumptions();
    leveraged.scenarios.conservative = { ...cheap.scenarios.conservative };
    leveraged.scenarios.base = { ...cheap.scenarios.base };
    leveraged.scenarios.optimistic = { ...cheap.scenarios.optimistic };

    const view = buildComparisonView([cheap, leveraged]);
    expect(view.rows[0].mortgage?.totalInitialAcquisitionCash).toBe(12_00_000);
    expect(view.rows[1].mortgage?.totalInitialAcquisitionCash).toBe(2_40_000);
    expect(view.rows[0].mortgage?.monthlyEmi).toBe(0);
    expect(view.rows[1].mortgage?.monthlyEmi).toBeGreaterThan(0);
    expect(view.rows[0].draft.downPaymentPercent).toBe(100);
  });

  it('reuses mortgage and scenario outputs for ranking and assessment', () => {
    const first = readyDraft();
    const second = { ...readyDraft(), id: 'p2', purchasePrice: 24_00_000, monthlyRent: 4_000 };
    const view = buildComparisonView([first, second]);
    expect(view.rows[0].base?.annualisedEquityIrr).toBeCloseTo(0.1, 6);
    expect(view.highlights.bestBaseIrrIds).toEqual(['p1']);
    expect(view.highlights.highestBaseNetProfitIds).toEqual(['p1']);
    expect(view.highlights.strongestConservativeIds).toEqual(['p1']);
    expect(view.rows[0].assessment?.baseIrr).toBe('positive');
    expect(view.rows[0].assessment?.baseProfit).toBe('positive');
    expect(view.rows[0].assessment?.annualCashFlow).toBe('positive');
  });

  it('handles negative cash flow, negative profit and unavailable IRR', () => {
    const negative = readyDraft();
    negative.monthlyRent = 1_000;
    negative.monthlyMaintenance = 20_000;
    negative.downPaymentPercent = 20;
    negative.holdingPeriodYears = 0;
    const row = buildComparisonRow(negative);
    expect(row.mortgage?.annualCashFlowAfterOperatingExpensesAndLoanPayments).toBeLessThan(0);
    expect(row.assessment?.annualCashFlow).toBe('negative');
    expect(row.base?.annualisedEquityIrr).toBeNull();
    expect(row.assessment?.baseIrr).toBe('unavailable');

    const loss = readyDraft();
    loss.monthlyRent = 0;
    loss.holdingPeriodYears = 2;
    loss.scenarios.conservative.sellingCostPercent = 10;
    loss.scenarios.base.sellingCostPercent = 10;
    loss.scenarios.optimistic.sellingCostPercent = 10;
    const lossRow = buildComparisonRow(loss);
    expect(lossRow.base?.netProfit).toBe(-1_20_000);
    expect(lossRow.assessment?.baseProfit).toBe('negative');
  });

  it('ranks the smallest cash-flow shortfall only among cash-flow-negative properties', () => {
    const weak = readyDraft();
    weak.id = 'weak';
    weak.monthlyRent = 1_000;
    weak.monthlyMaintenance = 8_000;
    weak.downPaymentPercent = 20;
    const worse = { ...weak, id: 'worse', monthlyMaintenance: 12_000 };
    const healthy = readyDraft();
    healthy.id = 'healthy';
    const view = buildComparisonView([weak, worse, healthy]);
    expect(view.highlights.lowestCashFlowShortfallIds).toEqual(['weak']);
  });

  it('rejects invalid prices, area, percentages and tenure by leaving the row unready', () => {
    const invalidPrice = { ...readyDraft(), purchasePrice: -1 };
    const invalidArea = { ...readyDraft(), superBuiltUpAreaSqFt: 0 };
    const invalidDown = { ...readyDraft(), downPaymentPercent: 120 };
    const invalidTenure = { ...readyDraft(), loanTenureYears: 0 };
    expect(buildComparisonRow(invalidPrice).ready).toBeFalse();
    expect(buildComparisonRow(invalidArea).ready).toBeFalse();
    expect(buildComparisonRow(invalidDown).ready).toBeFalse();
    expect(buildComparisonRow(invalidTenure).ready).toBeFalse();
  });

  it('keeps remaining property results stable after another property is removed', () => {
    const first = readyDraft();
    const second = { ...readyDraft(), id: 'p2', purchasePrice: 18_00_000 };
    const third = { ...readyDraft(), id: 'p3', monthlyRent: 12_000 };
    const before = buildComparisonView([first, second, third]);
    const after = buildComparisonView([first, third]);
    expect(after.rows.map((row) => row.id)).toEqual(['p1', 'p3']);
    expect(after.rows[0].snapshot).toEqual(before.rows[0].snapshot);
    expect(after.rows[0].mortgage).toEqual(before.rows[0].mortgage);
    expect(after.rows[1].snapshot).toEqual(before.rows[2].snapshot);
  });

  it('does not mutate shared scenario defaults when a sample property is edited', () => {
    const samples = createIllustrativeSampleProperties();
    samples[0].scenarios.base.propertyAppreciationPercent = 99;
    const again = createIllustrativeSampleProperties();
    expect(again[0].scenarios.base.propertyAppreciationPercent).toBe(5);
    expect(samples[0].downPaymentPercent).toBe(20);
    expect(samples[1].downPaymentPercent).toBe(25);
  });
});
