import { Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiProblem, ComparisonSummaryDto, SaveComparisonRequest, toSaveProperty } from '../core/comparison-api.models';
import { ComparisonApiService } from '../core/comparison-api.service';
import { ComparisonPropertyDraft } from '../core/comparison-models';
import { ComparisonWorkspaceService } from '../core/comparison.service';
import { SCENARIO_ORDER, ScenarioAssumptionRates, ScenarioName } from '../core/investment-scenarios';
import { formatInr, formatInrCompact, formatIrr, formatPercent } from '../core/inr-format';

@Component({
  selector: 'app-compare',
  imports: [ReactiveFormsModule],
  templateUrl: './compare.component.html',
  styleUrl: './compare.component.css',
})
export class CompareComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly workspace = inject(ComparisonWorkspaceService);
  private readonly api = inject(ComparisonApiService);
  private readonly destroyRef = inject(DestroyRef);
  private syncing = false;

  protected readonly formatInr = formatInr;
  protected readonly formatInrCompact = formatInrCompact;
  protected readonly formatPercent = formatPercent;
  protected readonly formatIrr = formatIrr;
  protected readonly scenarioOrder = SCENARIO_ORDER;
  protected readonly scenarioLabels: Record<ScenarioName, string> = {
    conservative: 'Conservative',
    base: 'Base',
    optimistic: 'Optimistic',
  };

  protected readonly form = this.formBuilder.nonNullable.group({
    analysisName: ['Untitled comparison', [Validators.required, Validators.maxLength(200)]],
    analysisDescription: ['', [Validators.maxLength(1000)]],
    properties: this.formBuilder.array<FormGroup>([]),
  });

  protected readonly view = this.workspace.view;
  protected readonly canAdd = this.workspace.canAdd;
  protected readonly hasProperties = computed(() => this.workspace.properties().length > 0);
  protected readonly dirty = this.workspace.dirty;
  protected readonly meta = this.workspace.meta;
  protected readonly busy = signal(false);
  protected readonly libraryLoading = signal(false);
  protected readonly statusMessage = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly fieldErrors = signal<string[]>([]);
  protected readonly library = signal<ComparisonSummaryDto[]>([]);
  protected readonly libraryError = signal<string | null>(null);

  ngOnInit(): void {
    const notice = this.workspace.consumePrefillNotice();
    if (notice) {
      this.statusMessage.set(notice);
    }
    this.rebuildFromWorkspace();
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.syncing) {
        return;
      }
      this.workspace.setMeta({
        name: this.form.controls.analysisName.getRawValue(),
        description: this.form.controls.analysisDescription.getRawValue(),
      });
      for (const group of this.properties.controls) {
        this.workspace.update(group.getRawValue().id as string, this.toDraft(group));
      }
    });
    void this.refreshLibrary();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.workspace.dirty()) {
      event.preventDefault();
      event.returnValue = true;
    }
  }

  protected get properties(): FormArray<FormGroup> {
    return this.form.controls.properties;
  }

  protected addProperty(): void {
    const draft = this.workspace.addEmpty();
    if (!draft) {
      return;
    }
    this.syncing = true;
    this.properties.push(this.buildGroup(draft), { emitEvent: false });
    this.syncing = false;
  }

  protected removeProperty(index: number): void {
    const id = this.properties.at(index).getRawValue().id as string;
    this.workspace.remove(id);
    this.syncing = true;
    this.properties.removeAt(index, { emitEvent: false });
    this.syncing = false;
  }

  protected loadSamples(): void {
    if (!this.confirmDiscard()) {
      return;
    }
    this.workspace.loadIllustrativeSamples();
    this.rebuildFromWorkspace();
    this.statusMessage.set('Loaded illustrative samples. Save them if you want to retrieve this comparison later.');
    this.errorMessage.set(null);
  }

  protected clearAll(): void {
    if (!this.confirmDiscard()) {
      return;
    }
    this.workspace.clear();
    this.rebuildFromWorkspace();
    this.statusMessage.set(null);
    this.errorMessage.set(null);
  }

  protected async save(): Promise<void> {
    if (this.busy() || this.form.invalid || this.workspace.properties().length === 0) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Complete the comparison name and each property before saving.');
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    this.fieldErrors.set([]);
    const request = this.toSaveRequest();
    const savedId = this.workspace.meta().savedId;

    try {
      const detail = savedId
        ? await firstValueFrom(this.api.update(savedId, request))
        : await firstValueFrom(this.api.create(request));
      this.workspace.markSaved(detail);
      this.rebuildFromWorkspace();
      this.statusMessage.set(savedId ? 'Comparison updated.' : 'Comparison saved.');
      await this.refreshLibrary();
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected async openSaved(id: string): Promise<void> {
    if (this.busy() || !this.confirmDiscard()) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const detail = await firstValueFrom(this.api.get(id));
      this.workspace.applySaved(detail);
      this.rebuildFromWorkspace();
      this.statusMessage.set(`Opened “${detail.name}”. Metrics were recalculated in the browser.`);
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected async deleteSaved(id: string, name: string): Promise<void> {
    if (this.busy()) {
      return;
    }
    if (!window.confirm(`Delete saved comparison “${name}”? This cannot be undone.`)) {
      return;
    }

    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      await firstValueFrom(this.api.delete(id));
      if (this.workspace.meta().savedId === id) {
        this.workspace.clear();
        this.rebuildFromWorkspace();
      }
      this.statusMessage.set(`Deleted “${name}”.`);
      await this.refreshLibrary();
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected namesById(ids: string[]): string {
    const names = this.view().rows
      .filter((row) => ids.includes(row.id))
      .map((row) => row.draft.name.trim() || 'Untitled property');
    return names.length ? names.join(', ') : 'Not available yet';
  }

  protected isLeader(ids: string[], id: string): boolean {
    return ids.includes(id);
  }

  protected formatTimestamp(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString('en-IN');
  }

  protected showError(group: FormGroup, controlName: string): boolean {
    const control = group.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  private async refreshLibrary(): Promise<void> {
    this.libraryLoading.set(true);
    this.libraryError.set(null);
    try {
      const items = await firstValueFrom(this.api.list());
      this.library.set(items);
    } catch (error) {
      const problem = error as ApiProblem;
      this.library.set([]);
      this.libraryError.set(problem.messages.join(' '));
    } finally {
      this.libraryLoading.set(false);
    }
  }

  private toSaveRequest(): SaveComparisonRequest {
    const meta = this.workspace.meta();
    return {
      name: this.form.controls.analysisName.getRawValue().trim(),
      description: this.form.controls.analysisDescription.getRawValue().trim() || null,
      expectedUpdatedUtc: meta.savedUpdatedUtc,
      properties: this.workspace.properties().map(toSaveProperty),
    };
  }

  private applyProblem(problem: ApiProblem): void {
    this.fieldErrors.set(problem.messages);
    this.errorMessage.set(
      problem.status === 409
        ? 'This comparison was changed elsewhere. Open it again before saving.'
        : problem.title,
    );
  }

  private confirmDiscard(): boolean {
    if (!this.workspace.dirty()) {
      return true;
    }
    return window.confirm('You have unsaved changes. Discard them and continue?');
  }

  private rebuildFromWorkspace(): void {
    this.syncing = true;
    this.form.controls.analysisName.setValue(this.workspace.meta().name, { emitEvent: false });
    this.form.controls.analysisDescription.setValue(this.workspace.meta().description, { emitEvent: false });
    this.properties.clear({ emitEvent: false });
    for (const draft of this.workspace.properties()) {
      this.properties.push(this.buildGroup(draft), { emitEvent: false });
    }
    this.syncing = false;
  }

  private buildGroup(draft: ComparisonPropertyDraft): FormGroup {
    return this.formBuilder.nonNullable.group({
      id: [draft.id],
      name: [draft.name, [Validators.required, Validators.maxLength(120)]],
      locality: [draft.locality, [Validators.maxLength(80)]],
      builderProject: [draft.builderProject, [Validators.maxLength(120)]],
      propertyType: [draft.propertyType, [Validators.required, Validators.maxLength(40)]],
      purchasePrice: this.formBuilder.control<number | null>(draft.purchasePrice, {
        validators: [Validators.required, Validators.min(1)],
      }),
      superBuiltUpAreaSqFt: this.formBuilder.control<number | null>(draft.superBuiltUpAreaSqFt, {
        validators: [Validators.required, Validators.min(1)],
      }),
      monthlyRent: this.formBuilder.control<number | null>(draft.monthlyRent, {
        validators: [Validators.required, Validators.min(0)],
      }),
      monthlyMaintenance: this.formBuilder.control<number | null>(draft.monthlyMaintenance, {
        validators: [Validators.required, Validators.min(0)],
      }),
      downPaymentPercent: [draft.downPaymentPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
      annualInterestRatePercent: [draft.annualInterestRatePercent, [Validators.required, Validators.min(0)]],
      loanTenureYears: [draft.loanTenureYears, [Validators.required, Validators.min(0.01)]],
      acquisitionCostPercent: [draft.acquisitionCostPercent, [Validators.required, Validators.min(0)]],
      annualPropertyTax: [draft.annualPropertyTax, [Validators.required, Validators.min(0)]],
      annualRepairAndMaintenance: [draft.annualRepairAndMaintenance, [Validators.required, Validators.min(0)]],
      vacancyPercent: [draft.vacancyPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
      holdingPeriodYears: [draft.holdingPeriodYears, [Validators.required, Validators.min(0)]],
      conservative: this.scenarioGroup(draft.scenarios.conservative),
      base: this.scenarioGroup(draft.scenarios.base),
      optimistic: this.scenarioGroup(draft.scenarios.optimistic),
    });
  }

  private scenarioGroup(defaults: ScenarioAssumptionRates): FormGroup {
    return this.formBuilder.nonNullable.group({
      propertyAppreciationPercent: [defaults.propertyAppreciationPercent, [Validators.required, Validators.min(0)]],
      rentGrowthPercent: [defaults.rentGrowthPercent, [Validators.required, Validators.min(0)]],
      vacancyPercent: [defaults.vacancyPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
      operatingExpenseGrowthPercent: [
        defaults.operatingExpenseGrowthPercent,
        [Validators.required, Validators.min(0)],
      ],
      sellingCostPercent: [defaults.sellingCostPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
    });
  }

  private toDraft(group: FormGroup): ComparisonPropertyDraft {
    const value = group.getRawValue();
    return {
      id: value.id,
      name: value.name,
      locality: value.locality,
      builderProject: value.builderProject,
      propertyType: value.propertyType,
      purchasePrice: value.purchasePrice,
      superBuiltUpAreaSqFt: value.superBuiltUpAreaSqFt,
      monthlyRent: value.monthlyRent,
      monthlyMaintenance: value.monthlyMaintenance,
      downPaymentPercent: value.downPaymentPercent,
      annualInterestRatePercent: value.annualInterestRatePercent,
      loanTenureYears: value.loanTenureYears,
      acquisitionCostPercent: value.acquisitionCostPercent,
      annualPropertyTax: value.annualPropertyTax,
      annualRepairAndMaintenance: value.annualRepairAndMaintenance,
      vacancyPercent: value.vacancyPercent,
      holdingPeriodYears: value.holdingPeriodYears,
      scenarios: {
        conservative: value.conservative,
        base: value.base,
        optimistic: value.optimistic,
      },
    };
  }
}
