export interface AnalystStatus {
  configured: boolean;
  provider: string;
  model: string;
  setupGuidance: string;
  disclaimer: string;
  hasGenuineLiveFeed: boolean;
}

export interface AnalystSection {
  key: string;
  title: string;
  body: string;
}

export interface AnalystEvidenceReference {
  kind: string;
  id?: string | null;
  label: string;
  href?: string | null;
  isSyntheticTestData: boolean;
  isStale: boolean;
  recordType?: string | null;
  verificationStatus?: string | null;
}

export interface AnalystAskResponse {
  status: 'answered' | 'not_configured' | string;
  disclaimer: string;
  setupGuidance?: string | null;
  sections: AnalystSection[];
  evidence: AnalystEvidenceReference[];
  warnings: string[];
  wroteToDatabase: boolean;
}

export interface AnalystSnapshotMetrics {
  pricePerSqFt?: number | null;
  grossAnnualRentalYieldPercent?: number | null;
  annualRentalIncome?: number | null;
  estimatedAnnualMaintenance?: number | null;
  netRentalIncomeBeforeOtherExpenses?: number | null;
}

export interface AnalystMortgageMetrics {
  downPaymentAmount?: number | null;
  loanPrincipal?: number | null;
  monthlyEmi?: number | null;
  annualLoanPayments?: number | null;
  acquisitionCostAmount?: number | null;
  totalInitialAcquisitionCash?: number | null;
  annualPotentialRent?: number | null;
  annualRentAfterVacancy?: number | null;
  annualOperatingExpenses?: number | null;
  annualCashFlowAfterOperatingExpensesAndLoanPayments?: number | null;
  outstandingLoanBalanceAfterHoldingPeriod?: number | null;
}

export interface AnalystScenarioMetrics {
  propertyAppreciationPercent?: number | null;
  rentGrowthPercent?: number | null;
  vacancyPercent?: number | null;
  operatingExpenseGrowthPercent?: number | null;
  sellingCostPercent?: number | null;
  initialCashInvested?: number | null;
  cumulativeRentalCashFlowAfterDebtService?: number | null;
  exitPropertyValue?: number | null;
  outstandingLoanAtExit?: number | null;
  sellingCosts?: number | null;
  netSaleProceeds?: number | null;
  totalCashReceived?: number | null;
  netProfit?: number | null;
  grossPropertyAppreciationPercent?: number | null;
  annualisedEquityIrr?: number | null;
  irrUnavailableReason?: string | null;
}

export interface AnalystPropertyContext {
  name?: string | null;
  locality?: string | null;
  builderProject?: string | null;
  propertyType?: string | null;
  purchasePrice?: number | null;
  superBuiltUpAreaSqFt?: number | null;
  monthlyRent?: number | null;
  monthlyMaintenance?: number | null;
  downPaymentPercent?: number | null;
  annualInterestRatePercent?: number | null;
  loanTenureYears?: number | null;
  acquisitionCostPercent?: number | null;
  holdingPeriodYears?: number | null;
  vacancyPercent?: number | null;
  annualPropertyTax?: number | null;
  annualRepairAndMaintenance?: number | null;
  snapshot?: AnalystSnapshotMetrics | null;
  mortgage?: AnalystMortgageMetrics | null;
  conservative?: AnalystScenarioMetrics | null;
  base?: AnalystScenarioMetrics | null;
  optimistic?: AnalystScenarioMetrics | null;
}

export interface AnalystAskRequest {
  question: string;
  comparisonId?: string | null;
  observationIds?: string[];
  includeMarketEvidence: boolean;
  properties: AnalystPropertyContext[];
}

export const ANALYST_EXAMPLE_QUESTIONS = [
  "Explain why this property's annual cash flow is negative.",
  'Compare these two apartments and explain the main differences in their investment returns.',
  'What happens if appreciation is lower than the Base scenario?',
  'What are the key risks in this analysis?',
  'Which assumptions have the biggest effect on the outcome?',
  'Summarise this analysis in a report I can review before making a decision.',
] as const;
