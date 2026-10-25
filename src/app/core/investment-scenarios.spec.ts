import { calculateOutstandingLoanBalance } from './investment-calculator';
import {
  calculateAllScenarios,
  calculateScenario,
  isValidScenarioPropertyInput,
  isValidScenarioRates,
  SCENARIO_ASSUMPTIONS,
} from './investment-scenarios';

const allEquityTwoYear = {
  purchasePrice: 12_00_000,
  monthlyRent: 10_000,
  monthlyMaintenance: 0,
  downPaymentPercent: 100,
  annualInterestRatePercent: 8.5,
  loanTenureYears: 20,
  acquisitionCostPercent: 0,
  annualPropertyTax: 0,
  annualRepairAndMaintenance: 0,
  holdingPeriodYears: 2,
};

const zeroRates = {
  propertyAppreciationPercent: 0,
  rentGrowthPercent: 0,
  vacancyPercent: 0,
  operatingExpenseGrowthPercent: 0,
  sellingCostPercent: 0,
};

describe('calculateScenario', () => {
  it('matches an independently calculated all-equity 10% IRR fixture', () => {
    /**
     * Independent worksheet (not using production functions):
     * Price 12,00,000; 100% cash; annual rent 1,20,000; two years;
     * no growth, vacancy, opex, selling costs or loan.
     * Year 0 equity CF = -12,00,000
     * Year 1 = 1,20,000
     * Year 2 = 1,20,000 + 12,00,000 sale = 13,20,000
     * NPV at 10% = -12,00,000 + 1,20,000/1.1 + 13,20,000/1.21 = 0
     */
    const result = calculateScenario(allEquityTwoYear, zeroRates, 'base');

    expect(result?.initialCashInvested).toBe(12_00_000);
    expect(result?.equityCashFlows).toEqual([-12_00_000, 1_20_000, 13_20_000]);
    expect(result?.cumulativeRentalCashFlowAfterDebtService).toBe(2_40_000);
    expect(result?.exitPropertyValue).toBe(12_00_000);
    expect(result?.outstandingLoanAtExit).toBe(0);
    expect(result?.sellingCosts).toBe(0);
    expect(result?.netSaleProceeds).toBe(12_00_000);
    expect(result?.totalCashReceived).toBe(14_40_000);
    expect(result?.netProfit).toBe(2_40_000);
    expect(result?.grossPropertyAppreciationPercent).toBe(0);
    expect(result?.annualisedEquityIrr).toBeCloseTo(0.1, 8);
  });

  it('does not double-count down payment, acquisition costs or loan repayment', () => {
    /**
     * Independent worksheet:
     * Price 10,00,000; down 20% = 2,00,000; acquisition 0; zero-interest 4-year loan
     * of 8,00,000; annual rent 1,00,000; opex 0; hold 2 years; no growth/vacancy/selling costs.
     * Annual loan payment = 8,00,000 / 4 = 2,00,000
     * Outstanding after 2 years = 8,00,000 × (1 − 24/48) = 4,00,000
     * Year 0 = -2,00,000
     * Year 1 = 1,00,000 − 2,00,000 = -1,00,000
     * Year 2 operations = -1,00,000; sale proceeds = 10,00,000 − 4,00,000 = 6,00,000
     * Year 2 equity CF = 5,00,000
     * Let x = 1+r. Quadratic 2x² + x − 5 = 0 ⇒ x = (−1 + √41)/4, r = x − 1 ≈ 35.0781%
     */
    const result = calculateScenario(
      {
        purchasePrice: 10_00_000,
        monthlyRent: 1_00_000 / 12,
        monthlyMaintenance: 0,
        downPaymentPercent: 20,
        annualInterestRatePercent: 0,
        loanTenureYears: 4,
        acquisitionCostPercent: 0,
        annualPropertyTax: 0,
        annualRepairAndMaintenance: 0,
        holdingPeriodYears: 2,
      },
      zeroRates,
      'base',
    );

    expect(result?.initialCashInvested).toBe(2_00_000);
    expect(result?.equityCashFlows[0]).toBe(-2_00_000);
    expect(result?.years[1].annualLoanPayments).toBe(2_00_000);
    expect(result?.years[1].cashFlowAfterDebtService).toBe(-1_00_000);
    expect(result?.outstandingLoanAtExit).toBe(4_00_000);
    expect(result?.netSaleProceeds).toBe(6_00_000);
    expect(result?.equityCashFlows[2]).toBe(5_00_000);
    expect(result?.netProfit).toBe(2_00_000);
    expect(result?.annualisedEquityIrr).toBeCloseTo((-1 + Math.sqrt(41)) / 4 - 1, 6);
  });

  it('keeps property value unchanged when appreciation is zero', () => {
    const result = calculateScenario(allEquityTwoYear, zeroRates, 'conservative');
    expect(result?.years.every((year) => year.propertyValue === 12_00_000)).toBeTrue();
  });

  it('keeps rent unchanged when rent growth is zero and applies zero vacancy', () => {
    const result = calculateScenario(allEquityTwoYear, zeroRates, 'base');
    expect(result?.years[1].potentialRent).toBe(1_20_000);
    expect(result?.years[2].potentialRent).toBe(1_20_000);
    expect(result?.years[1].effectiveRent).toBe(1_20_000);
  });

  it('charges no selling costs when the rate is zero', () => {
    const result = calculateScenario(allEquityTwoYear, zeroRates, 'optimistic');
    expect(result?.sellingCosts).toBe(0);
    expect(result?.netSaleProceeds).toBe(result?.exitPropertyValue);
  });

  it('adds sale proceeds only in the terminal year', () => {
    const result = calculateScenario(allEquityTwoYear, zeroRates, 'base');
    expect(result?.years[0].netSaleProceeds).toBe(0);
    expect(result?.years[1].netSaleProceeds).toBe(0);
    expect(result?.years[2].netSaleProceeds).toBe(12_00_000);
    expect(result?.equityCashFlows[1]).toBe(result?.years[1].cashFlowAfterDebtService);
    expect(result?.equityCashFlows[2]).toBe(
      (result?.years[2].cashFlowAfterDebtService ?? 0) + 12_00_000,
    );
  });

  it('uses amortised outstanding loan, not straight-line, when interest is charged', () => {
    const result = calculateScenario(
      {
        ...allEquityTwoYear,
        downPaymentPercent: 20,
        acquisitionCostPercent: 0,
        holdingPeriodYears: 10,
        annualInterestRatePercent: 8.5,
        loanTenureYears: 20,
        purchasePrice: 1_20_00_000,
        monthlyRent: 40_000,
      },
      zeroRates,
      'base',
    );
    const amortised = calculateOutstandingLoanBalance(96_00_000, 8.5, 20, 10);
    expect(result?.outstandingLoanAtExit).toBeCloseTo(amortised, 2);
    expect(result!.outstandingLoanAtExit).not.toBe(96_00_000 * 0.5);
    expect(result!.outstandingLoanAtExit).toBeGreaterThan(0);
  });

  it('sets loan payments to zero after the tenure and never lets the balance go negative', () => {
    const shorter = calculateScenario(
      { ...allEquityTwoYear, downPaymentPercent: 20, holdingPeriodYears: 1, loanTenureYears: 20 },
      zeroRates,
      'base',
    );
    const equal = calculateScenario(
      {
        ...allEquityTwoYear,
        downPaymentPercent: 20,
        holdingPeriodYears: 2,
        loanTenureYears: 2,
        annualInterestRatePercent: 0,
      },
      zeroRates,
      'base',
    );
    const longer = calculateScenario(
      {
        ...allEquityTwoYear,
        downPaymentPercent: 20,
        holdingPeriodYears: 3,
        loanTenureYears: 2,
        annualInterestRatePercent: 0,
      },
      zeroRates,
      'base',
    );

    expect(shorter!.outstandingLoanAtExit).toBeGreaterThan(0);
    expect(equal?.outstandingLoanAtExit).toBe(0);
    expect(equal?.years[2].annualLoanPayments).toBeGreaterThan(0);
    expect(longer?.outstandingLoanAtExit).toBe(0);
    expect(longer?.years[3].annualLoanPayments).toBe(0);
    expect(longer!.years.every((year) => year.outstandingLoan >= 0)).toBeTrue();
  });

  it('allows negative operating cash flow while still counting sale proceeds', () => {
    const result = calculateScenario(
      {
        ...allEquityTwoYear,
        monthlyRent: 1_000,
        monthlyMaintenance: 8_000,
      },
      zeroRates,
      'base',
    );
    expect(result!.years[1].cashFlowAfterDebtService).toBeLessThan(0);
    expect(result!.cumulativeRentalCashFlowAfterDebtService).toBeLessThan(0);
    expect(result!.netSaleProceeds).toBe(12_00_000);
  });

  it('orders optimistic above base above conservative when only appreciation differs', () => {
    const property = { ...allEquityTwoYear, holdingPeriodYears: 5 };
    const conservative = calculateScenario(
      property,
      { ...zeroRates, propertyAppreciationPercent: 0 },
      'conservative',
    );
    const base = calculateScenario(
      property,
      { ...zeroRates, propertyAppreciationPercent: 5 },
      'base',
    );
    const optimistic = calculateScenario(
      property,
      { ...zeroRates, propertyAppreciationPercent: 10 },
      'optimistic',
    );

    expect(conservative!.exitPropertyValue).toBe(12_00_000);
    expect(base!.exitPropertyValue).toBeCloseTo(12_00_000 * Math.pow(1.05, 5), 2);
    expect(optimistic!.exitPropertyValue).toBeCloseTo(12_00_000 * Math.pow(1.1, 5), 2);
    expect(optimistic!.netProfit).toBeGreaterThan(base!.netProfit);
    expect(base!.netProfit).toBeGreaterThan(conservative!.netProfit);
  });

  it('marks IRR unavailable for a zero holding period and for invalid rates', () => {
    const immediate = calculateScenario(
      { ...allEquityTwoYear, holdingPeriodYears: 0 },
      zeroRates,
      'base',
    );
    expect(immediate?.annualisedEquityIrr).toBeNull();
    expect(immediate?.irrUnavailableReason).toContain('Holding period is zero');
    expect(immediate?.equityCashFlows).toHaveSize(1);

    expect(calculateScenario(allEquityTwoYear, { ...zeroRates, vacancyPercent: 120 }, 'base')).toBeNull();
    expect(isValidScenarioRates({ ...zeroRates, propertyAppreciationPercent: -1 })).toBeFalse();
    expect(isValidScenarioPropertyInput({ ...allEquityTwoYear, holdingPeriodYears: -1 })).toBeFalse();
    expect(isValidScenarioPropertyInput({ ...allEquityTwoYear, holdingPeriodYears: 1.5 })).toBeFalse();
  });

  it('builds all three default scenarios for the same property', () => {
    const results = calculateAllScenarios(allEquityTwoYear, SCENARIO_ASSUMPTIONS);
    expect(results?.map((item) => item.name)).toEqual(['conservative', 'base', 'optimistic']);
    expect(results?.every((item) => item.initialCashInvested === 12_00_000)).toBeTrue();
  });
});
