import { ComparisonPropertyDraft } from './comparison-models';
import { ScenarioAssumptionRates } from './investment-scenarios';

export interface ComparisonSummaryDto {
  id: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string;
  propertyCount: number;
  schemaVersion: number;
}

export interface ScenarioAssumptionsDto {
  propertyAppreciationPercent: number;
  rentGrowthPercent: number;
  vacancyPercent: number;
  operatingExpenseGrowthPercent: number;
  sellingCostPercent: number;
}

export interface ComparedPropertyDto {
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
  holdingPeriodYears: number;
  vacancyPercent: number;
  annualPropertyTax: number;
  annualRepairAndMaintenance: number;
  conservative: ScenarioAssumptionsDto;
  base: ScenarioAssumptionsDto;
  optimistic: ScenarioAssumptionsDto;
}

export interface ComparisonDetailDto {
  id: string;
  name: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string;
  schemaVersion: number;
  properties: ComparedPropertyDto[];
}

export interface SaveComparedPropertyRequest extends Omit<ComparedPropertyDto, 'id'> {
  id?: string | null;
}

export interface SaveComparisonRequest {
  name: string;
  description?: string | null;
  expectedUpdatedUtc?: string | null;
  properties: SaveComparedPropertyRequest[];
}

export interface ApiProblem {
  title: string;
  messages: string[];
  status: number;
}

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function isPersistedPropertyId(id: string): boolean {
  return guidPattern.test(id);
}

export function toDraft(property: ComparedPropertyDto): ComparisonPropertyDraft {
  return {
    id: property.id,
    name: property.name,
    locality: property.locality,
    builderProject: property.builderProject,
    propertyType: property.propertyType,
    purchasePrice: property.purchasePrice,
    superBuiltUpAreaSqFt: property.superBuiltUpAreaSqFt,
    monthlyRent: property.monthlyRent,
    monthlyMaintenance: property.monthlyMaintenance,
    downPaymentPercent: property.downPaymentPercent,
    annualInterestRatePercent: property.annualInterestRatePercent,
    loanTenureYears: property.loanTenureYears,
    acquisitionCostPercent: property.acquisitionCostPercent,
    annualPropertyTax: property.annualPropertyTax,
    annualRepairAndMaintenance: property.annualRepairAndMaintenance,
    vacancyPercent: property.vacancyPercent,
    holdingPeriodYears: property.holdingPeriodYears,
    scenarios: {
      conservative: toRates(property.conservative),
      base: toRates(property.base),
      optimistic: toRates(property.optimistic),
    },
  };
}

export function toSaveProperty(draft: ComparisonPropertyDraft): SaveComparedPropertyRequest {
  return {
    id: isPersistedPropertyId(draft.id) ? draft.id : null,
    name: draft.name,
    locality: draft.locality,
    builderProject: draft.builderProject,
    propertyType: draft.propertyType,
    purchasePrice: draft.purchasePrice,
    superBuiltUpAreaSqFt: draft.superBuiltUpAreaSqFt,
    monthlyRent: draft.monthlyRent,
    monthlyMaintenance: draft.monthlyMaintenance,
    downPaymentPercent: draft.downPaymentPercent,
    annualInterestRatePercent: draft.annualInterestRatePercent,
    loanTenureYears: draft.loanTenureYears,
    acquisitionCostPercent: draft.acquisitionCostPercent,
    holdingPeriodYears: draft.holdingPeriodYears,
    vacancyPercent: draft.vacancyPercent,
    annualPropertyTax: draft.annualPropertyTax,
    annualRepairAndMaintenance: draft.annualRepairAndMaintenance,
    conservative: draft.scenarios.conservative,
    base: draft.scenarios.base,
    optimistic: draft.scenarios.optimistic,
  };
}

function toRates(dto: ScenarioAssumptionsDto): ScenarioAssumptionRates {
  return { ...dto };
}
