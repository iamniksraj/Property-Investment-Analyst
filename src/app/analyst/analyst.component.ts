import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription, firstValueFrom } from 'rxjs';
import {
  ANALYST_EXAMPLE_QUESTIONS,
  AnalystAskResponse,
  AnalystStatus,
} from '../core/analyst-api.models';
import { AnalystApiService } from '../core/analyst-api.service';
import { toAnalystProperties } from '../core/analyst-context';
import { ApiProblem, ComparisonSummaryDto, toDraft } from '../core/comparison-api.models';
import { ComparisonApiService } from '../core/comparison-api.service';
import { buildComparisonView } from '../core/comparison-engine';
import { ComparisonWorkspaceService } from '../core/comparison.service';

@Component({
  selector: 'app-analyst',
  imports: [FormsModule, RouterLink],
  templateUrl: './analyst.component.html',
  styleUrl: './analyst.component.css',
})
export class AnalystComponent implements OnInit {
  private readonly analystApi = inject(AnalystApiService);
  private readonly comparisonApi = inject(ComparisonApiService);
  private readonly workspace = inject(ComparisonWorkspaceService);

  protected readonly examples = ANALYST_EXAMPLE_QUESTIONS;
  protected readonly status = signal<AnalystStatus | null>(null);
  protected readonly saved = signal<ComparisonSummaryDto[]>([]);
  protected readonly answer = signal<AnalystAskResponse | null>(null);
  protected readonly error = signal<ApiProblem | null>(null);
  protected readonly loading = signal(false);
  protected readonly history = signal<{ question: string; answer: AnalystAskResponse }[]>([]);
  protected readonly copied = signal(false);

  question = '';
  contextKey = 'workspace';
  includeMarketEvidence = true;
  private inflight: Subscription | null = null;

  ngOnInit(): void {
    this.analystApi.status().subscribe({
      next: (value) => this.status.set(value),
      error: (problem: ApiProblem) => this.error.set(problem),
    });
    this.comparisonApi.list().subscribe({
      next: (value) => this.saved.set(value),
      error: () => this.saved.set([]),
    });
  }

  protected workspaceCount(): number {
    return this.workspace.properties().length;
  }

  protected useExample(text: string): void {
    this.question = text;
  }

  protected cancel(): void {
    this.inflight?.unsubscribe();
    this.inflight = null;
    this.loading.set(false);
  }

  async ask(): Promise<void> {
    const question = this.question.trim();
    if (!question || this.loading()) {
      return;
    }

    this.cancel();
    this.loading.set(true);
    this.error.set(null);
    this.answer.set(null);
    this.copied.set(false);

    let payload;
    try {
      payload = await this.buildRequest(question);
    } catch (problem) {
      this.loading.set(false);
      this.error.set(problem as ApiProblem);
      return;
    }

    this.inflight = this.analystApi.ask(payload).subscribe({
      next: (value) => {
        this.loading.set(false);
        this.inflight = null;
        this.answer.set(value);
        if (value.status === 'answered') {
          this.history.update((current) => [{ question, answer: value }, ...current].slice(0, 8));
        }
      },
      error: (problem: ApiProblem) => {
        this.loading.set(false);
        this.inflight = null;
        this.error.set(problem);
      },
    });
  }

  protected reportText(): string {
    const answer = this.answer();
    if (!answer) {
      return '';
    }

    const lines = [
      'Property Investment Analyst — AI report',
      answer.disclaimer,
      '',
      ...answer.warnings,
      '',
      ...answer.sections.flatMap((section) => [section.title, section.body, '']),
      'Evidence',
      ...answer.evidence.map((item) =>
        `${item.label}${item.isSyntheticTestData ? ' (synthetic — not genuine market evidence)' : ''}`,
      ),
    ];
    return lines.join('\n');
  }

  protected async copyReport(): Promise<void> {
    const text = this.reportText();
    if (!text) {
      return;
    }

    await navigator.clipboard.writeText(text);
    this.copied.set(true);
  }

  protected printReport(): void {
    window.print();
  }

  private async buildRequest(question: string) {
    if (this.contextKey === 'none') {
      return {
        question,
        includeMarketEvidence: this.includeMarketEvidence,
        properties: [],
      };
    }

    if (this.contextKey === 'workspace') {
      return {
        question,
        comparisonId: this.workspace.meta().savedId,
        includeMarketEvidence: this.includeMarketEvidence,
        properties: toAnalystProperties(this.workspace.view().rows),
      };
    }

    const savedId = this.contextKey.startsWith('saved:') ? this.contextKey.slice('saved:'.length) : '';
    const loaded = await firstValueFrom(this.comparisonApi.get(savedId));
    const view = buildComparisonView(loaded.properties.map(toDraft));
    return {
      question,
      comparisonId: loaded.id,
      includeMarketEvidence: this.includeMarketEvidence,
      properties: toAnalystProperties(view.rows),
    };
  }
}
