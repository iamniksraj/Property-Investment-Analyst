import { Injectable, computed, signal } from '@angular/core';
import {
  buildComparisonView,
  createEmptyPropertyDraft,
  createIllustrativeSampleProperties,
} from './comparison-engine';
import { ComparisonDetailDto, toDraft } from './comparison-api.models';
import { ComparisonPropertyDraft, MAX_COMPARISON_PROPERTIES } from './comparison-models';

export interface ComparisonWorkspaceMeta {
  name: string;
  description: string;
  savedId: string | null;
  savedUpdatedUtc: string | null;
  schemaVersion: number;
}

@Injectable({ providedIn: 'root' })
export class ComparisonWorkspaceService {
  readonly maxProperties = MAX_COMPARISON_PROPERTIES;
  private readonly drafts = signal<ComparisonPropertyDraft[]>([]);
  private nextId = 1;

  private readonly metaState = signal<ComparisonWorkspaceMeta>({
    name: 'Untitled comparison',
    description: '',
    savedId: null,
    savedUpdatedUtc: null,
    schemaVersion: 1,
  });

  readonly dirty = signal(false);
  readonly prefillNotice = signal<string | null>(null);
  readonly properties = this.drafts.asReadonly();
  readonly meta = this.metaState.asReadonly();
  readonly view = computed(() => buildComparisonView(this.drafts()));
  readonly canAdd = computed(() => this.drafts().length < this.maxProperties);

  setMeta(patch: Partial<Pick<ComparisonWorkspaceMeta, 'name' | 'description'>>): void {
    this.metaState.update((current) => ({ ...current, ...patch }));
    this.dirty.set(true);
  }

  addFromObservation(partial: ComparisonPropertyDraft, notice: string): ComparisonPropertyDraft | null {
    const created = this.addEmpty();
    if (!created) {
      return null;
    }
    this.update(created.id, {
      ...created,
      name: partial.name,
      locality: partial.locality,
      builderProject: partial.builderProject,
      propertyType: partial.propertyType,
      purchasePrice: partial.purchasePrice,
      superBuiltUpAreaSqFt: partial.superBuiltUpAreaSqFt,
      monthlyRent: partial.monthlyRent,
    });
    this.prefillNotice.set(notice);
    return this.drafts().find((item) => item.id === created.id) ?? null;
  }

  consumePrefillNotice(): string | null {
    const notice = this.prefillNotice();
    this.prefillNotice.set(null);
    return notice;
  }

  addEmpty(): ComparisonPropertyDraft | null {
    if (!this.canAdd()) {
      return null;
    }
    const draft = createEmptyPropertyDraft(`property-${this.nextId}`);
    this.nextId += 1;
    this.drafts.update((current) => [...current, draft]);
    this.dirty.set(true);
    return draft;
  }

  update(id: string, patch: ComparisonPropertyDraft): void {
    this.drafts.update((current) =>
      current.map((item) => (item.id === id ? { ...patch, id } : item)),
    );
    this.dirty.set(true);
  }

  remove(id: string): void {
    this.drafts.update((current) => current.filter((item) => item.id !== id));
    this.dirty.set(true);
  }

  loadIllustrativeSamples(): void {
    const samples = createIllustrativeSampleProperties();
    this.replaceDrafts(samples);
    this.metaState.set({
      name: 'Illustrative Bengaluru sample',
      description: 'Demonstration cases only — not live listings.',
      savedId: null,
      savedUpdatedUtc: null,
      schemaVersion: 1,
    });
    this.dirty.set(true);
  }

  clear(): void {
    this.replaceDrafts([]);
    this.metaState.set({
      name: 'Untitled comparison',
      description: '',
      savedId: null,
      savedUpdatedUtc: null,
      schemaVersion: 1,
    });
    this.dirty.set(false);
  }

  applySaved(detail: ComparisonDetailDto): void {
    this.replaceDrafts(detail.properties.map(toDraft));
    this.metaState.set({
      name: detail.name,
      description: detail.description ?? '',
      savedId: detail.id,
      savedUpdatedUtc: detail.updatedUtc,
      schemaVersion: detail.schemaVersion,
    });
    this.dirty.set(false);
  }

  markSaved(detail: ComparisonDetailDto): void {
    this.applySaved(detail);
  }

  private replaceDrafts(next: ComparisonPropertyDraft[]): void {
    this.drafts.set(next);
    const maxLocal = next
      .map((item) => /^property-(\d+)$/.exec(item.id)?.[1])
      .filter((value): value is string => !!value)
      .map((value) => Number(value))
      .reduce((highest, value) => Math.max(highest, value), 0);
    this.nextId = maxLocal + 1;
  }
}
