import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiProblem } from '../core/comparison-api.models';
import { ComparisonWorkspaceService } from '../core/comparison.service';
import { formatInr } from '../core/inr-format';
import { MarketApiService, MarketQuery } from '../core/market-api.service';
import {
  AREA_TYPE_LABELS,
  AssumptionComparisonResponse,
  MarketImportResult,
  MarketObservationDto,
  MarketProviderStatusResponse,
  MarketSearchResponse,
  MarketStatsResponse,
  RECORD_TYPE_LABELS,
} from '../core/market-api.models';
import { draftFromObservation } from '../core/market-prefill';

@Component({
  selector: 'app-market',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './market.component.html',
  styleUrl: './market.component.css',
})
export class MarketComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(MarketApiService);
  private readonly workspace = inject(ComparisonWorkspaceService);
  private readonly router = inject(Router);

  protected readonly formatInr = formatInr;
  protected readonly workspaceProperties = this.workspace.properties;
  protected readonly pageSize = 20;

  protected recordLabel(type: string): string {
    return RECORD_TYPE_LABELS[type] || type;
  }

  protected areaLabel(type: string): string {
    return AREA_TYPE_LABELS[type] || type;
  }

  protected readonly busy = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly fieldErrors = signal<string[]>([]);
  protected readonly statusMessage = signal<string | null>(null);
  protected readonly searchResult = signal<MarketSearchResponse | null>(null);
  protected readonly stats = signal<MarketStatsResponse | null>(null);
  protected readonly selected = signal<MarketObservationDto | null>(null);
  protected readonly preview = signal<MarketImportResult | null>(null);
  protected readonly assumption = signal<AssumptionComparisonResponse | null>(null);
  protected readonly providerStatus = signal<MarketProviderStatusResponse | null>(null);
  readonly selectedPropertyId = signal<string>('');
  protected readonly acknowledgeIncompatible = signal(false);
  protected readonly canAdd = this.workspace.canAdd;
  private selectedFile: File | null = null;

  protected readonly form = this.formBuilder.nonNullable.group({
    locality: ['Whitefield', [Validators.required, Validators.minLength(2)]],
    propertyType: ['Apartment'],
    configuration: [''],
    recordType: [''],
    areaType: [''],
    areaUnit: [''],
    verificationStatus: [''],
    includeSynthetic: [true],
    includeOutliers: [true],
    observedFrom: [''],
    observedTo: [''],
  });

  protected readonly historyBars = computed(() => {
    const points = this.stats()?.history.points ?? [];
    const max = Math.max(1, ...points.map((item) => item.count));
    return points.map((item) => ({
      ...item,
      heightPercent: Math.round((item.count / max) * 100),
    }));
  });

  ngOnInit(): void {
    void this.loadProviderStatus();
  }

  async loadProviderStatus(): Promise<void> {
    try {
      this.providerStatus.set(await firstValueFrom(this.api.providers()));
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    }
  }

  async search(skip = 0): Promise<void> {
    if (this.form.controls.locality.invalid) {
      this.form.controls.locality.markAsTouched();
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    this.selected.set(null);
    this.assumption.set(null);
    const query = this.toQuery(skip);
    try {
      const [search, stats] = await Promise.all([
        firstValueFrom(this.api.search({ ...query, includeOutliers: this.form.controls.includeOutliers.value, take: this.pageSize })),
        firstValueFrom(this.api.stats({ ...query, includeOutliers: false })),
      ]);
      this.searchResult.set(search);
      this.stats.set(stats);
      if (search.totalCount === 0) {
        this.statusMessage.set('No observations for this filter. These figures are not live market coverage.');
      } else if (stats.onlySyntheticAvailable) {
        this.statusMessage.set(
          `Loaded ${search.totalCount} observation${search.totalCount === 1 ? '' : 's'}. Only synthetic test data is available — no genuine market conclusion can be drawn.`,
        );
      } else {
        this.statusMessage.set(`Loaded ${search.totalCount} observation${search.totalCount === 1 ? '' : 's'}.`);
      }
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected async nextPage(): Promise<void> {
    const current = this.searchResult();
    if (!current || current.skip + current.take >= current.totalCount) {
      return;
    }
    await this.search(current.skip + current.take);
  }

  protected async previousPage(): Promise<void> {
    const current = this.searchResult();
    if (!current || current.skip === 0) {
      return;
    }
    await this.search(Math.max(0, current.skip - current.take));
  }

  protected inspect(item: MarketObservationDto): void {
    this.selected.set(item);
  }

  async useInComparison(item: MarketObservationDto): Promise<void> {
    if (!this.workspace.canAdd()) {
      this.errorMessage.set(
        'The comparison workspace already has three properties. Remove one before adding this observation.',
      );
      return;
    }
    const { draft, warnings } = draftFromObservation(item);
    const confirmed = window.confirm(
      `Add a new comparison property from this ${this.recordLabel(item.recordType)} observation?\n\n`
        + 'Existing comparison inputs will not be overwritten.\n\n'
        + (warnings.length ? `Warnings:\n- ${warnings.join('\n- ')}` : 'No extra quality warnings.'),
    );
    if (!confirmed) {
      return;
    }
    const added = this.workspace.addFromObservation(
      draft,
      `Added “${draft.name}” from a ${this.recordLabel(item.recordType)} observation. Review the inputs; they were not saved automatically.`,
    );
    if (!added) {
      this.errorMessage.set('Could not add the property to the comparison workspace.');
      return;
    }
    await this.router.navigateByUrl('/compare');
  }

  async compareAssumption(): Promise<void> {
    const property = this.workspace.properties().find((item) => item.id === this.selectedPropertyId());
    if (!property) {
      this.errorMessage.set('Select a property from the comparison workspace first. This does not change its inputs.');
      return;
    }
    if (this.form.controls.locality.invalid) {
      this.form.controls.locality.markAsTouched();
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    const value = this.form.getRawValue();
    try {
      this.assumption.set(
        await firstValueFrom(
          this.api.compareAssumption({
            locality: value.locality.trim(),
            propertyType: property.propertyType || value.propertyType || undefined,
            configuration: value.configuration || undefined,
            includeSynthetic: value.includeSynthetic,
            acknowledgeIncompatibleArea: this.acknowledgeIncompatible(),
            purchasePrice: property.purchasePrice,
            monthlyRent: property.monthlyRent,
            areaValue: property.superBuiltUpAreaSqFt,
            areaType: property.superBuiltUpAreaSqFt ? 'super_built_up' : undefined,
            areaUnit: property.superBuiltUpAreaSqFt ? 'sq_ft' : undefined,
          }),
        ),
      );
      this.statusMessage.set(
        'Compared the selected property assumptions with observations. Purchase price, rent and financing were not changed.',
      );
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.preview.set(null);
  }

  protected async runPreview(): Promise<void> {
    if (!this.selectedFile) {
      this.errorMessage.set('Choose a CSV file first.');
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      this.preview.set(await firstValueFrom(this.api.preview(this.selectedFile)));
      this.statusMessage.set('Preview only — nothing was written until you confirm the import.');
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  protected async runCommit(): Promise<void> {
    if (!this.selectedFile || !this.preview()) {
      this.errorMessage.set('Preview the CSV before committing.');
      return;
    }
    if (!window.confirm('Import the valid, non-duplicate rows? Invalid rows will be skipped. This cannot silently overwrite existing observations.')) {
      return;
    }
    this.busy.set(true);
    this.errorMessage.set(null);
    try {
      const result = await firstValueFrom(this.api.commit(this.selectedFile));
      this.preview.set(result);
      this.statusMessage.set(
        `Imported ${result.inserted} row(s). Skipped ${result.duplicates} duplicate(s) and ${result.invalid} invalid row(s).`,
      );
    } catch (error) {
      this.applyProblem(error as ApiProblem);
    } finally {
      this.busy.set(false);
    }
  }

  private toQuery(skip: number): MarketQuery {
    const value = this.form.getRawValue();
    return {
      locality: value.locality.trim(),
      propertyType: value.propertyType || undefined,
      configuration: value.configuration || undefined,
      recordType: value.recordType || undefined,
      areaType: value.areaType || undefined,
      areaUnit: value.areaUnit || undefined,
      verificationStatus: value.verificationStatus || undefined,
      includeSynthetic: value.includeSynthetic,
      observedFrom: value.observedFrom || undefined,
      observedTo: value.observedTo || undefined,
      skip,
    };
  }

  private applyProblem(problem: ApiProblem): void {
    this.fieldErrors.set(problem.messages);
    this.errorMessage.set(problem.title);
  }
}
