import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { AnalystAskResponse, AnalystStatus } from '../core/analyst-api.models';
import { ComparisonWorkspaceService } from '../core/comparison.service';
import { createIllustrativeSampleProperties } from '../core/comparison-engine';
import { AnalystComponent } from './analyst.component';

describe('AnalystComponent', () => {
  const base = environment.apiBaseUrl.replace(/\/$/, '');
  let http: HttpTestingController;
  let workspace: ComparisonWorkspaceService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnalystComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    workspace = TestBed.inject(ComparisonWorkspaceService);
  });

  afterEach(() => http.verify());

  it('shows the not-configured state and example questions', () => {
    const fixture = TestBed.createComponent(AnalystComponent);
    fixture.detectChanges();
    flushBootstrap(http, base, { configured: false });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('AI is not configured');
    expect(text).toContain('InvestmentAnalyst__OpenAI__ApiKey');
    expect(text).toContain('key risks');
  });

  it('submits a workspace question, shows loading, then renders sections and evidence', async () => {
    workspace.loadIllustrativeSamples();
    const price = workspace.properties()[0].purchasePrice;
    const fixture = TestBed.createComponent(AnalystComponent);
    fixture.detectChanges();
    flushBootstrap(http, base, { configured: true });
    fixture.detectChanges();

    fixture.componentInstance.question = 'Explain why this property\'s annual cash flow is negative.';
    const pending = fixture.componentInstance.ask();
    await Promise.resolve();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Asking');

    const request = http.expectOne(`${base}/api/investment-analyst/ask`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.question).toContain('cash flow');
    expect(request.request.body.properties.length).toBeGreaterThan(0);
    request.flush(answered());
    await pending;
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('Year-one cash flow is negative');
    expect(text).toContain('Synthetic — not genuine market evidence');
    expect(text).toContain('Asking price, not a completed transaction');
    expect(workspace.properties()[0].purchasePrice).toBe(price);
    expect(workspace.properties().map((item) => item.name)).toEqual(
      createIllustrativeSampleProperties().map((item) => item.name),
    );
  });

  it('renders validation errors and does not change workspace inputs', async () => {
    workspace.loadIllustrativeSamples();
    const names = workspace.properties().map((item) => item.name);
    const fixture = TestBed.createComponent(AnalystComponent);
    fixture.detectChanges();
    flushBootstrap(http, base, { configured: true });
    fixture.componentInstance.question = 'What are the key risks?';
    const pending = fixture.componentInstance.ask();
    await Promise.resolve();
    http.expectOne(`${base}/api/investment-analyst/ask`).flush(
      { title: 'Validation failed', errors: { question: ['Enter a question.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await pending;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Enter a question.');
    expect(workspace.properties().map((item) => item.name)).toEqual(names);
  });

  it('renders provider failures', async () => {
    const fixture = TestBed.createComponent(AnalystComponent);
    fixture.detectChanges();
    flushBootstrap(http, base, { configured: true });
    fixture.componentInstance.question = 'Summarise this analysis.';
    const pending = fixture.componentInstance.ask();
    await Promise.resolve();
    http.expectOne(`${base}/api/investment-analyst/ask`).flush(
      { title: 'AI provider timed out', detail: 'The AI provider timed out. No fabricated answer was returned.' },
      { status: 502, statusText: 'Bad Gateway' },
    );
    await pending;
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('AI provider timed out');
  });

  it('selects a saved comparison without writing it into the workspace', async () => {
    const fixture = TestBed.createComponent(AnalystComponent);
    fixture.detectChanges();
    flushBootstrap(http, base, { configured: true }, [
      {
        id: '11111111-1111-4111-8111-111111111111',
        name: 'Saved desk',
        description: null,
        createdUtc: '2026-01-01T00:00:00Z',
        updatedUtc: '2026-01-01T00:00:00Z',
        propertyCount: 1,
        schemaVersion: 1,
      },
    ]);
    fixture.componentInstance.contextKey = 'saved:11111111-1111-4111-8111-111111111111';
    fixture.componentInstance.question = 'Compare these two apartments.';
    const pending = fixture.componentInstance.ask();
    const get = await waitForRequest(http, `${base}/api/comparisons/11111111-1111-4111-8111-111111111111`);
    get.flush({
      id: '11111111-1111-4111-8111-111111111111',
      name: 'Saved desk',
      description: null,
      createdUtc: '2026-01-01T00:00:00Z',
      updatedUtc: '2026-01-01T00:00:00Z',
      schemaVersion: 1,
      properties: [],
    });
    const ask = await waitForRequest(http, `${base}/api/investment-analyst/ask`);
    expect(ask.request.body.comparisonId).toBe('11111111-1111-4111-8111-111111111111');
    ask.flush(answered());
    await pending;
    expect(workspace.properties().length).toBe(0);
  });
});

async function waitForRequest(http: HttpTestingController, url: string) {
  for (let attempt = 0; attempt < 10; attempt += 1) {
    const matches = http.match(url);
    if (matches.length === 1) {
      return matches[0];
    }
    await Promise.resolve();
  }
  return http.expectOne(url);
}

function flushBootstrap(
  http: HttpTestingController,
  base: string,
  status: Partial<AnalystStatus>,
  saved: unknown[] = [],
): void {
  http.expectOne(`${base}/api/investment-analyst/status`).flush({
    configured: false,
    provider: 'OpenAI Chat Completions',
    model: '',
    setupGuidance: 'Set InvestmentAnalyst__OpenAI__ApiKey on the ASP.NET host.',
    disclaimer: 'Not financial advice.',
    hasGenuineLiveFeed: false,
    ...status,
  });
  http.expectOne(`${base}/api/comparisons`).flush(saved);
}

function answered(): AnalystAskResponse {
  return {
    status: 'answered',
    disclaimer: 'Not financial advice.',
    sections: [
      { key: 'summary', title: 'Summary', body: 'Year-one cash flow is negative after debt service.' },
      { key: 'calculatedNumbers', title: 'What the calculated numbers show', body: 'Gross yield is supplied.' },
    ],
    evidence: [
      {
        kind: 'observation',
        id: '11111111-1111-4111-8111-111111111111',
        label: 'Whitefield asking_price',
        href: '/market',
        isSyntheticTestData: true,
        isStale: false,
        recordType: 'asking_price',
        verificationStatus: 'source_cited',
      },
    ],
    warnings: ['Only synthetic test observations are available.'],
    wroteToDatabase: false,
  };
}
