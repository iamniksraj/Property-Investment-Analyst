import { AnalystPropertyContext, AnalystScenarioMetrics } from './analyst-api.models';
import { ComparisonRow } from './comparison-models';
import { ScenarioResult } from './investment-scenarios';

export function toAnalystProperties(rows: ComparisonRow[]): AnalystPropertyContext[] {
  return rows.slice(0, 3).map((row) => ({
    name: row.draft.name,
    locality: row.draft.locality,
    builderProject: row.draft.builderProject,
    propertyType: row.draft.propertyType,
    purchasePrice: row.draft.purchasePrice,
    superBuiltUpAreaSqFt: row.draft.superBuiltUpAreaSqFt,
    monthlyRent: row.draft.monthlyRent,
    monthlyMaintenance: row.draft.monthlyMaintenance,
    downPaymentPercent: row.draft.downPaymentPercent,
    annualInterestRatePercent: row.draft.annualInterestRatePercent,
    loanTenureYears: row.draft.loanTenureYears,
    acquisitionCostPercent: row.draft.acquisitionCostPercent,
    holdingPeriodYears: row.draft.holdingPeriodYears,
    vacancyPercent: row.draft.vacancyPercent,
    annualPropertyTax: row.draft.annualPropertyTax,
    annualRepairAndMaintenance: row.draft.annualRepairAndMaintenance,
    snapshot: row.snapshot,
    mortgage: row.mortgage,
    conservative: toScenario(row.conservative),
    base: toScenario(row.base),
    optimistic: toScenario(row.optimistic),
  }));
}

function toScenario(result: ScenarioResult | null): AnalystScenarioMetrics | null {
  if (!result) {
    return null;
  }

  return {
    propertyAppreciationPercent: result.assumptions.propertyAppreciationPercent,
    rentGrowthPercent: result.assumptions.rentGrowthPercent,
    vacancyPercent: result.assumptions.vacancyPercent,
    operatingExpenseGrowthPercent: result.assumptions.operatingExpenseGrowthPercent,
    sellingCostPercent: result.assumptions.sellingCostPercent,
    initialCashInvested: result.initialCashInvested,
    cumulativeRentalCashFlowAfterDebtService: result.cumulativeRentalCashFlowAfterDebtService,
    exitPropertyValue: result.exitPropertyValue,
    outstandingLoanAtExit: result.outstandingLoanAtExit,
    sellingCosts: result.sellingCosts,
    netSaleProceeds: result.netSaleProceeds,
    totalCashReceived: result.totalCashReceived,
    netProfit: result.netProfit,
    grossPropertyAppreciationPercent: result.grossPropertyAppreciationPercent,
    annualisedEquityIrr: result.annualisedEquityIrr,
    irrUnavailableReason: result.irrUnavailableReason,
  };
}
