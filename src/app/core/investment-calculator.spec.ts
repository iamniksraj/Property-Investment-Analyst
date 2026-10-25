import {
  calculateInvestmentMetrics,
  calculateMonthlyEmi,
  calculateMortgageCashFlow,
  calculateOutstandingLoanBalance,
  isValidInvestmentInput,
  isValidMortgageCashFlowInput,
} from './investment-calculator';

describe('calculateInvestmentMetrics', () => {
  const validInput = {
    purchasePrice: 1_20_00_000,
    superBuiltUpAreaSqFt: 1_200,
    monthlyRent: 40_000,
    monthlyMaintenance: 5_000,
  };

  it('calculates price per sq. ft., yield, rent and maintenance', () => {
    const metrics = calculateInvestmentMetrics(validInput);

    expect(metrics).toEqual({
      pricePerSqFt: 10_000,
      annualRentalIncome: 4_80_000,
      estimatedAnnualMaintenance: 60_000,
      grossAnnualRentalYieldPercent: 4,
      netRentalIncomeBeforeOtherExpenses: 4_20_000,
    });
  });

  it('allows zero rent and maintenance', () => {
    const metrics = calculateInvestmentMetrics({
      ...validInput,
      monthlyRent: 0,
      monthlyMaintenance: 0,
    });

    expect(metrics).toEqual({
      pricePerSqFt: 10_000,
      annualRentalIncome: 0,
      estimatedAnnualMaintenance: 0,
      grossAnnualRentalYieldPercent: 0,
      netRentalIncomeBeforeOtherExpenses: 0,
    });
  });

  it('can return a negative net income when maintenance exceeds rent', () => {
    const metrics = calculateInvestmentMetrics({
      ...validInput,
      monthlyRent: 4_000,
      monthlyMaintenance: 8_000,
    });

    expect(metrics?.annualRentalIncome).toBe(48_000);
    expect(metrics?.estimatedAnnualMaintenance).toBe(96_000);
    expect(metrics?.netRentalIncomeBeforeOtherExpenses).toBe(-48_000);
  });

  it('returns null for empty, NaN, infinite or non-positive purchase inputs', () => {
    expect(calculateInvestmentMetrics(null)).toBeNull();
    expect(calculateInvestmentMetrics({})).toBeNull();
    expect(calculateInvestmentMetrics({ ...validInput, purchasePrice: 0 })).toBeNull();
    expect(calculateInvestmentMetrics({ ...validInput, superBuiltUpAreaSqFt: -10 })).toBeNull();
    expect(calculateInvestmentMetrics({ ...validInput, purchasePrice: Number.NaN })).toBeNull();
    expect(calculateInvestmentMetrics({ ...validInput, monthlyRent: Number.POSITIVE_INFINITY })).toBeNull();
    expect(calculateInvestmentMetrics({ ...validInput, monthlyMaintenance: -1 })).toBeNull();
  });

  it('accepts numeric strings from form controls', () => {
    const metrics = calculateInvestmentMetrics({
      purchasePrice: '15000000' as unknown as number,
      superBuiltUpAreaSqFt: '1500' as unknown as number,
      monthlyRent: '45000' as unknown as number,
      monthlyMaintenance: '6000' as unknown as number,
    });

    expect(metrics?.pricePerSqFt).toBe(10_000);
    expect(metrics?.annualRentalIncome).toBe(5_40_000);
    expect(metrics?.estimatedAnnualMaintenance).toBe(72_000);
    expect(metrics?.grossAnnualRentalYieldPercent).toBeCloseTo(3.6, 10);
    expect(metrics?.netRentalIncomeBeforeOtherExpenses).toBe(4_68_000);
  });

  it('rejects incomplete numeric input', () => {
    expect(
      isValidInvestmentInput({
        purchasePrice: 1_00_00_000,
        superBuiltUpAreaSqFt: 1_000,
      }),
    ).toBeFalse();
  });
});

describe('calculateMortgageCashFlow', () => {
  const sampleInput = {
    purchasePrice: 1_20_00_000,
    monthlyRent: 40_000,
    monthlyMaintenance: 5_000,
    downPaymentPercent: 20,
    annualInterestRatePercent: 8.5,
    loanTenureYears: 20,
    acquisitionCostPercent: 7,
    annualPropertyTax: 0,
    annualRepairAndMaintenance: 0,
    vacancyPercent: 5,
    holdingPeriodYears: 10,
  };

  it('calculates the 1.2 crore sample with starter assumptions', () => {
    const result = calculateMortgageCashFlow(sampleInput);
    const expectedEmi = calculateMonthlyEmi(96_00_000, 8.5, 20);

    expect(result?.downPaymentAmount).toBe(24_00_000);
    expect(result?.loanPrincipal).toBe(96_00_000);
    expect(result?.monthlyEmi).toBeCloseTo(expectedEmi, 2);
    expect(result?.monthlyEmi).toBe(83_311.03);
    expect(result?.annualLoanPayments).toBe(9_99_732.36);
    expect(result?.acquisitionCostAmount).toBe(8_40_000);
    expect(result?.totalInitialAcquisitionCash).toBe(32_40_000);
    expect(result?.annualPotentialRent).toBe(4_80_000);
    expect(result?.annualRentAfterVacancy).toBe(4_56_000);
    expect(result?.annualOperatingExpenses).toBe(60_000);
    expect(result?.annualCashFlowAfterOperatingExpensesAndLoanPayments).toBe(-6_03_732.36);
    expect(result?.outstandingLoanBalanceAfterHoldingPeriod).toBeCloseTo(
      calculateOutstandingLoanBalance(96_00_000, 8.5, 20, 10),
      2,
    );
    expect(result!.outstandingLoanBalanceAfterHoldingPeriod).toBe(67_19_406.99);
  });

  it('uses the standard amortisation formula for a known EMI', () => {
    // Principal 1,00,000; 12% annual; 1 year → monthly rate 1%, 12 instalments.
    const emi = calculateMonthlyEmi(1_00_000, 12, 1);
    const compound = Math.pow(1.01, 12);
    expect(emi).toBeCloseTo((1_00_000 * 0.01 * compound) / (compound - 1), 10);
    expect(calculateOutstandingLoanBalance(1_00_000, 12, 1, 1)).toBe(0);
    expect(calculateOutstandingLoanBalance(1_00_000, 12, 1, 0)).toBe(1_00_000);
  });

  it('splits a zero-interest loan evenly and never goes negative', () => {
    const metrics = calculateMortgageCashFlow({
      ...sampleInput,
      annualInterestRatePercent: 0,
      loanTenureYears: 2,
      holdingPeriodYears: 1,
    });

    expect(metrics?.monthlyEmi).toBe(4_00_000);
    expect(metrics?.outstandingLoanBalanceAfterHoldingPeriod).toBe(48_00_000);
    expect(calculateOutstandingLoanBalance(96_00_000, 0, 2, 3)).toBe(0);
    expect(calculateOutstandingLoanBalance(96_00_000, 8.5, 20, 25)).toBe(0);
    expect(calculateOutstandingLoanBalance(96_00_000, 8.5, 20, 20)).toBe(0);
  });

  it('handles 0% and 100% down payments and zero vacancy', () => {
    const allLoan = calculateMortgageCashFlow({
      ...sampleInput,
      downPaymentPercent: 0,
      vacancyPercent: 0,
    });
    expect(allLoan?.downPaymentAmount).toBe(0);
    expect(allLoan?.loanPrincipal).toBe(1_20_00_000);
    expect(allLoan?.annualRentAfterVacancy).toBe(4_80_000);
    expect(allLoan?.totalInitialAcquisitionCash).toBe(8_40_000);

    const allCash = calculateMortgageCashFlow({
      ...sampleInput,
      downPaymentPercent: 100,
    });
    expect(allCash?.loanPrincipal).toBe(0);
    expect(allCash?.monthlyEmi).toBe(0);
    expect(allCash?.outstandingLoanBalanceAfterHoldingPeriod).toBe(0);
    expect(allCash?.totalInitialAcquisitionCash).toBe(1_20_00_000 + 8_40_000);
    expect(allCash?.annualCashFlowAfterOperatingExpensesAndLoanPayments).toBe(4_56_000 - 60_000);
  });

  it('includes tax and repairs in operating expenses', () => {
    const metrics = calculateMortgageCashFlow({
      ...sampleInput,
      annualPropertyTax: 18_000,
      annualRepairAndMaintenance: 24_000,
    });
    expect(metrics?.annualOperatingExpenses).toBe(1_02_000);
  });

  it('rejects invalid mortgage inputs', () => {
    expect(calculateMortgageCashFlow(null)).toBeNull();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, downPaymentPercent: -1 })).toBeFalse();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, downPaymentPercent: 101 })).toBeFalse();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, vacancyPercent: 101 })).toBeFalse();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, loanTenureYears: 0 })).toBeFalse();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, holdingPeriodYears: -1 })).toBeFalse();
    expect(isValidMortgageCashFlowInput({ ...sampleInput, annualInterestRatePercent: -0.1 })).toBeFalse();
    expect(calculateMortgageCashFlow({ ...sampleInput, purchasePrice: 0 })).toBeNull();
    expect(calculateMortgageCashFlow({ ...sampleInput, monthlyRent: Number.NaN })).toBeNull();
  });
});
