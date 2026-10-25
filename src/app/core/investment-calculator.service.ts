import { Injectable } from '@angular/core';
import {
  calculateInvestmentMetrics,
  calculateMortgageCashFlow,
  MortgageCashFlowInput,
  MortgageCashFlowMetrics,
  PropertyInvestmentInput,
  PropertyInvestmentMetrics,
} from './investment-calculator';
import {
  calculateAllScenarios,
  ScenarioAssumptionRates,
  ScenarioName,
  ScenarioPropertyInput,
  ScenarioResult,
} from './investment-scenarios';

@Injectable({ providedIn: 'root' })
export class InvestmentCalculatorService {
  calculate(
    input: Partial<PropertyInvestmentInput> | null | undefined,
  ): PropertyInvestmentMetrics | null {
    return calculateInvestmentMetrics(input);
  }

  calculateMortgage(
    input: Partial<MortgageCashFlowInput> | null | undefined,
  ): MortgageCashFlowMetrics | null {
    return calculateMortgageCashFlow(input);
  }

  calculateScenarios(
    property: Partial<ScenarioPropertyInput> | null | undefined,
    ratesByScenario: Partial<Record<ScenarioName, Partial<ScenarioAssumptionRates>>> | null | undefined,
  ): ScenarioResult[] | null {
    return calculateAllScenarios(property, ratesByScenario);
  }
}
