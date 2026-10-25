import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { AnalystApiService } from './analyst-api.service';
import { AnalystAskResponse, AnalystStatus } from './analyst-api.models';
import { ApiProblem } from './comparison-api.models';

describe('AnalystApiService', () => {
  let service: AnalystApiService;
  let http: HttpTestingController;
  const base = environment.apiBaseUrl.replace(/\/$/, '');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AnalystApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads status without sending credentials', () => {
    let result: AnalystStatus | undefined;
    service.status().subscribe((value) => (result = value));
    const request = http.expectOne(`${base}/api/investment-analyst/status`);
    expect(request.request.method).toBe('GET');
    expect(JSON.stringify(request.request.body)).not.toContain('ApiKey');
    request.flush({
      configured: false,
      provider: 'OpenAI Chat Completions',
      model: '',
      setupGuidance: 'Set InvestmentAnalyst__OpenAI__ApiKey',
      disclaimer: 'Not advice.',
      hasGenuineLiveFeed: false,
    } satisfies AnalystStatus);
    expect(result?.configured).toBeFalse();
    expect(result?.hasGenuineLiveFeed).toBeFalse();
  });

  it('posts a question and maps provider failures', () => {
    let problem: ApiProblem | undefined;
    service
      .ask({ question: 'What are the key risks?', includeMarketEvidence: true, properties: [] })
      .subscribe({ error: (value) => (problem = value) });
    const request = http.expectOne(`${base}/api/investment-analyst/ask`);
    expect(request.request.method).toBe('POST');
    request.flush({ title: 'AI provider timed out', detail: 'Timed out.' }, { status: 502, statusText: 'Bad Gateway' });
    expect(problem?.title).toBe('AI provider timed out');
    expect(problem?.messages).toContain('Timed out.');
  });

  it('returns an answered payload', () => {
    let result: AnalystAskResponse | undefined;
    service
      .ask({ question: 'Summarise this analysis.', includeMarketEvidence: false, properties: [] })
      .subscribe((value) => (result = value));
    http.expectOne(`${base}/api/investment-analyst/ask`).flush({
      status: 'answered',
      disclaimer: 'Not advice.',
      sections: [{ key: 'summary', title: 'Summary', body: 'Cash flow is negative.' }],
      evidence: [],
      warnings: [],
      wroteToDatabase: false,
    } satisfies AnalystAskResponse);
    expect(result?.status).toBe('answered');
    expect(result?.wroteToDatabase).toBeFalse();
  });
});
