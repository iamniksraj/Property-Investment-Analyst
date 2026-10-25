import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { InvestmentCalculatorService } from '../core/investment-calculator.service';
import {
  MORTGAGE_ASSUMPTIONS,
  MortgageCashFlowMetrics,
  PropertyInvestmentMetrics,
} from '../core/investment-calculator';
import {
  SCENARIO_ASSUMPTIONS,
  SCENARIO_ORDER,
  ScenarioAssumptionRates,
  ScenarioName,
  ScenarioResult,
} from '../core/investment-scenarios';
import { formatInr, formatIrr, formatPercent } from '../core/inr-format';

@Component({
  selector: 'app-dashboard',
  imports: [ReactiveFormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly calculator = inject(InvestmentCalculatorService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly propertyType = 'Apartment';
  protected readonly formatInr = formatInr;
  protected readonly formatPercent = formatPercent;
  protected readonly formatIrr = formatIrr;
  protected readonly scenarioOrder = SCENARIO_ORDER;
  protected readonly scenarioLabels: Record<ScenarioName, string> = {
    conservative: 'Conservative',
    base: 'Base',
    optimistic: 'Optimistic',
  };

  protected submitted = false;
  protected metrics: PropertyInvestmentMetrics | null = null;
  protected mortgage: MortgageCashFlowMetrics | null = null;
  protected scenarios: ScenarioResult[] | null = null;
  protected readonly assumptions = MORTGAGE_ASSUMPTIONS;

  protected readonly form = this.formBuilder.nonNullable.group({
    propertyName: ['', [Validators.required, Validators.maxLength(120)]],
    city: ['Bengaluru', [Validators.required, Validators.maxLength(80)]],
    locality: ['', [Validators.required, Validators.maxLength(80)]],
    purchasePrice: this.formBuilder.control<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    superBuiltUpArea: this.formBuilder.control<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    expectedMonthlyRent: this.formBuilder.control<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    monthlyMaintenance: this.formBuilder.control<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    downPaymentPercent: this.formBuilder.control<number>(MORTGAGE_ASSUMPTIONS.downPaymentPercent, {
      validators: [Validators.required, Validators.min(0), Validators.max(100)],
    }),
    annualInterestRatePercent: this.formBuilder.control<number>(
      MORTGAGE_ASSUMPTIONS.annualInterestRatePercent,
      { validators: [Validators.required, Validators.min(0)] },
    ),
    loanTenureYears: this.formBuilder.control<number>(MORTGAGE_ASSUMPTIONS.loanTenureYears, {
      validators: [Validators.required, Validators.min(0.01)],
    }),
    acquisitionCostPercent: this.formBuilder.control<number>(
      MORTGAGE_ASSUMPTIONS.acquisitionCostPercent,
      { validators: [Validators.required, Validators.min(0)] },
    ),
    annualPropertyTax: this.formBuilder.control<number>(MORTGAGE_ASSUMPTIONS.annualPropertyTax, {
      validators: [Validators.required, Validators.min(0)],
    }),
    annualRepairAndMaintenance: this.formBuilder.control<number>(
      MORTGAGE_ASSUMPTIONS.annualRepairAndMaintenance,
      { validators: [Validators.required, Validators.min(0)] },
    ),
    vacancyPercent: this.formBuilder.control<number>(MORTGAGE_ASSUMPTIONS.vacancyPercent, {
      validators: [Validators.required, Validators.min(0), Validators.max(100)],
    }),
    holdingPeriodYears: this.formBuilder.control<number>(MORTGAGE_ASSUMPTIONS.holdingPeriodYears, {
      validators: [Validators.required, Validators.min(0)],
    }),
    conservative: this.buildScenarioGroup(SCENARIO_ASSUMPTIONS.conservative),
    base: this.buildScenarioGroup(SCENARIO_ASSUMPTIONS.base),
    optimistic: this.buildScenarioGroup(SCENARIO_ASSUMPTIONS.optimistic),
  });

  private buildScenarioGroup(defaults: ScenarioAssumptionRates): FormGroup {
    return this.formBuilder.nonNullable.group({
      propertyAppreciationPercent: [
        defaults.propertyAppreciationPercent,
        [Validators.required, Validators.min(0)],
      ],
      rentGrowthPercent: [defaults.rentGrowthPercent, [Validators.required, Validators.min(0)]],
      vacancyPercent: [
        defaults.vacancyPercent,
        [Validators.required, Validators.min(0), Validators.max(100)],
      ],
      operatingExpenseGrowthPercent: [
        defaults.operatingExpenseGrowthPercent,
        [Validators.required, Validators.min(0)],
      ],
      sellingCostPercent: [
        defaults.sellingCostPercent,
        [Validators.required, Validators.min(0), Validators.max(100)],
      ],
    });
  }

  ngOnInit(): void {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshMetrics());
  }

  protected showError(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty || this.submitted);
  }

  protected showScenarioError(
    scenario: ScenarioName,
    field: keyof ScenarioAssumptionRates,
  ): boolean {
    const control = this.form.controls[scenario].get(field);
    return !!control && control.invalid && (control.touched || control.dirty || this.submitted);
  }

  protected profitBarWidth(netProfit: number): number {
    const peak = Math.max(
      1,
      ...(this.scenarios ?? []).map((scenario) => Math.abs(scenario.netProfit)),
    );
    return (Math.abs(netProfit) / peak) * 100;
  }

  protected onSubmit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();
    this.refreshMetrics();
  }

  private refreshMetrics(): void {
    const value = this.form.getRawValue();
    const purchasePrice = value.purchasePrice ?? Number.NaN;
    const monthlyRent = value.expectedMonthlyRent ?? Number.NaN;
    const monthlyMaintenance = value.monthlyMaintenance ?? Number.NaN;

    this.metrics = this.calculator.calculate({
      purchasePrice,
      superBuiltUpAreaSqFt: value.superBuiltUpArea ?? Number.NaN,
      monthlyRent,
      monthlyMaintenance,
    });

    this.mortgage = this.calculator.calculateMortgage({
      purchasePrice,
      monthlyRent,
      monthlyMaintenance,
      downPaymentPercent: value.downPaymentPercent ?? Number.NaN,
      annualInterestRatePercent: value.annualInterestRatePercent ?? Number.NaN,
      loanTenureYears: value.loanTenureYears ?? Number.NaN,
      acquisitionCostPercent: value.acquisitionCostPercent ?? Number.NaN,
      annualPropertyTax: value.annualPropertyTax ?? Number.NaN,
      annualRepairAndMaintenance: value.annualRepairAndMaintenance ?? Number.NaN,
      vacancyPercent: value.vacancyPercent ?? Number.NaN,
      holdingPeriodYears: value.holdingPeriodYears ?? Number.NaN,
    });

    this.scenarios = this.calculator.calculateScenarios(
      {
        purchasePrice,
        monthlyRent,
        monthlyMaintenance,
        downPaymentPercent: value.downPaymentPercent ?? Number.NaN,
        annualInterestRatePercent: value.annualInterestRatePercent ?? Number.NaN,
        loanTenureYears: value.loanTenureYears ?? Number.NaN,
        acquisitionCostPercent: value.acquisitionCostPercent ?? Number.NaN,
        annualPropertyTax: value.annualPropertyTax ?? Number.NaN,
        annualRepairAndMaintenance: value.annualRepairAndMaintenance ?? Number.NaN,
        holdingPeriodYears: value.holdingPeriodYears ?? Number.NaN,
      },
      {
        conservative: value.conservative,
        base: value.base,
        optimistic: value.optimistic,
      },
    );
  }
}
