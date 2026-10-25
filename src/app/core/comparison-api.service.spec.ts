import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ComparisonApiService } from './comparison-api.service';
import { ComparisonDetailDto, SaveComparisonRequest } from './comparison-api.models';

describe('ComparisonApiService', () => {
  let service: ComparisonApiService;
  let http: HttpTestingController;
  const base = environment.apiBaseUrl.replace(/\/$/, '');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ComparisonApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads a saved comparison on success', () => {
    const detail = {
      id: '11111111-1111-4111-8111-111111111111',
      name: 'Desk',
      description: null,
      createdUtc: '2026-01-01T00:00:00Z',
      updatedUtc: '2026-01-01T00:00:00Z',
      schemaVersion: 1,
      properties: [],
    } satisfies ComparisonDetailDto;

    let result: ComparisonDetailDto | undefined;
    service.get(detail.id).subscribe((value) => (result = value));

    const request = http.expectOne(`${base}/api/comparisons/${detail.id}`);
    expect(request.request.method).toBe('GET');
    request.flush(detail);
    expect(result?.name).toBe('Desk');
  });

  it('creates a comparison and surfaces validation errors', () => {
    const payload = { name: ' ', properties: [] } satisfies SaveComparisonRequest;
    const messages: string[] = [];
    service.create(payload).subscribe({
      error: (problem) => messages.push(...problem.messages),
    });

    const request = http.expectOne(`${base}/api/comparisons`);
    expect(request.request.method).toBe('POST');
    request.flush(
      { title: 'Validation failed', errors: { name: ['A comparison name is required.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(messages).toEqual(['A comparison name is required.']);
  });

  it('maps unreachable API errors', () => {
    let status = -1;
    service.list().subscribe({
      error: (problem) => (status = problem.status),
    });

    http.expectOne(`${base}/api/comparisons`).error(new ProgressEvent('error'));
    expect(status).toBe(0);
  });
});
