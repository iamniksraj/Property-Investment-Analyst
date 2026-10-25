import { MortgageCashFlowMetrics, PropertyInvestmentMetrics } from './investment-calculator';
import { ScenarioAssumptionRates, ScenarioName, ScenarioResult } from './investment-scenarios';

export const MAX_COMPARISON_PROPERTIES = 3;

export interface ComparisonPropertyDraft {
  id: string;
  name: string;
  locality: string;
  builderProject: string;
  propertyType: string;
  purchasePrice: number | null;
  superBuiltUpAreaSqFt: number | null;
  monthlyRent: number | null;
  monthlyMaintenance: number | null;
  downPaymentPercent: number;
  annualInterestRatePercent: number;
  loanTenureYears: number;
  acquisitionCostPercent: number;
  annualPropertyTax: number;
  annualRepairAndMaintenance: number;
  vacancyPercent: number;
  holdingPeriodYears: number;
  scenarios: Record<ScenarioName, ScenarioAssumptionRates>;
}

export type SignAssessment = 'positive' | 'negative' | 'zero';

export interface PropertyAssessment {
  annualCashFlow: SignAssessment;
  baseProfit: SignAssessment;
  baseIrr: SignAssessment | 'unavailable';
  conservativeSensitivity: 'more-resilient' | 'more-sensitive' | 'similar' | 'unavailable';
  notes: string[];
}

export interface ComparisonRow {
  id: string;
  draft: ComparisonPropertyDraft;
  ready: boolean;
  snapshot: PropertyInvestmentMetrics | null;
  mortgage: MortgageCashFlowMetrics | null;
  scenarios: ScenarioResult[] | null;
  base: ScenarioResult | null;
  conservative: ScenarioResult | null;
  optimistic: ScenarioResult | null;
  assessment: PropertyAssessment | null;
}

export interface ComparisonHighlights {
  highestGrossYieldIds: string[];
  lowestInitialCashIds: string[];
  bestBaseIrrIds: string[];
  strongestConservativeIds: string[];
  highestBaseNetProfitIds: string[];
  lowestCashFlowShortfallIds: string[];
}

export interface ComparisonView {
  rows: ComparisonRow[];
  highlights: ComparisonHighlights;
}
