import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MarketApiService } from './market-api.service';
import { MarketSearchResponse } from './market-api.models';

describe('MarketApiService', () => {
  let service: MarketApiService;
  let http: HttpTestingController;
  const base = environment.apiBaseUrl.replace(/\/$/, '');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MarketApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('searches observations by locality', () => {
    const payload: MarketSearchResponse = {
      items: [],
      count: 0,
      totalCount: 0,
      skip: 0,
      take: 50,
      oldestObservedOn: null,
      newestObservedOn: null,
      isSparse: true,
      containsStale: false,
      containsUnverified: false,
      containsSynthetic: false,
      onlySyntheticAvailable: false,
    };
    let result: MarketSearchResponse | undefined;
    service.search({ locality: 'Whitefield' }).subscribe((value) => (result = value));
    const request = http.expectOne(`${base}/api/market-observations?locality=Whitefield`);
    expect(request.request.method).toBe('GET');
    request.flush(payload);
    expect(result?.isSparse).toBeTrue();
  });

  it('loads provider status without exposing credentials', () => {
    let live: boolean | undefined;
    service.providers().subscribe((value) => (live = value.hasGenuineLiveFeed));
    const request = http.expectOne(`${base}/api/market-providers`);
    expect(request.request.method).toBe('GET');
    request.flush({
      hasGenuineLiveFeed: false,
      allowManualRefresh: false,
      ingestionPolicy: 'Insert-only.',
      genuineObservationCount: 0,
      syntheticObservationCount: 0,
      newestObservationDate: null,
      lastSuccessfulImportUtc: null,
      lastImport: null,
      providers: [],
      notes: 'No genuine live feed.',
    });
    expect(live).toBeFalse();
  });

  it('posts an assumption comparison without altering query strings', () => {
    let disclaimer: string | undefined;
    service
      .compareAssumption({
        locality: 'Whitefield',
        includeSynthetic: true,
        acknowledgeIncompatibleArea: false,
        purchasePrice: 12_000_000,
        areaValue: 1200,
        areaType: 'super_built_up',
        areaUnit: 'sq_ft',
      })
      .subscribe((value) => (disclaimer = value.disclaimer));
    const request = http.expectOne(`${base}/api/market-observations/assumption-comparison`);
    expect(request.request.method).toBe('POST');
    request.flush({
      usingSyntheticTestData: true,
      disclaimer: 'Not a valuation.',
      askingPrice: null,
      advertisedRent: null,
    });
    expect(disclaimer).toBe('Not a valuation.');
  });

  it('surfaces import validation errors', () => {
    const messages: string[] = [];
    service.preview(new File(['x'], 'bad.csv', { type: 'text/csv' })).subscribe({
      error: (problem) => messages.push(...problem.messages),
    });
    const request = http.expectOne(`${base}/api/market-observations/import/preview`);
    request.flush(
      { title: 'Validation failed', errors: { file: ['Upload a CSV file.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(messages).toEqual(['Upload a CSV file.']);
  });
});
