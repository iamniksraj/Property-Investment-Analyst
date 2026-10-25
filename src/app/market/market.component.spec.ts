import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { ComparisonWorkspaceService } from '../core/comparison.service';
import {
  AssumptionComparisonResponse,
  MarketProviderStatusResponse,
  MarketSearchResponse,
  MarketStatsResponse,
} from '../core/market-api.models';
import { MarketComponent } from './market.component';

describe('MarketComponent', () => {
  const base = environment.apiBaseUrl.replace(/\/$/, '');
  let http: HttpTestingController;
  let workspace: ComparisonWorkspaceService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarketComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    workspace = TestBed.inject(ComparisonWorkspaceService);
  });

  afterEach(() => http.verify());

  it('renders empty and synthetic-only states from the API', async () => {
    const fixture = TestBed.createComponent(MarketComponent);
    fixture.detectChanges();
    flushProviders(http, base);
    const pending = fixture.componentInstance.search();
    flushSearch(http, emptySearch(), syntheticStats());
    await pending;
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent || '';
    expect(text).toContain('No rows to list.');
    expect(text).toContain('Synthetic-only statistic');
    expect(text).toContain('Insufficient history');
  });

  it('compares a workspace property without changing its purchase price', async () => {
    const created = workspace.addEmpty();
    workspace.update(created!.id, { ...created!, name: 'Keep me', purchasePrice: 12_000_000, superBuiltUpAreaSqFt: 1200 });
    const fixture = TestBed.createComponent(MarketComponent);
    fixture.detectChanges();
    flushProviders(http, base);
    fixture.componentInstance.selectedPropertyId.set(created!.id);
    const pending = fixture.componentInstance.compareAssumption();
    const request = http.expectOne(`${base}/api/market-observations/assumption-comparison`);
    expect(request.request.method).toBe('POST');
    const payload: AssumptionComparisonResponse = {
      usingSyntheticTestData: true,
      disclaimer: 'Not a valuation.',
      askingPrice: {
        recordType: 'asking_price',
        compared: true,
        status: 'compared',
        comparableCount: 8,
        userAmount: 12_000_000,
        userPerSqFt: 10_000,
        medianAmount: 12_000_000,
        percentile25: 11_500_000,
        percentile75: 12_800_000,
        medianPerSqFt: 10_000,
        amountDifference: 0,
        amountDifferencePercent: 0,
        perSqFtDifference: 0,
        perSqFtDifferencePercent: 0,
        oldestObservedOn: '2026-10-01',
        newestObservedOn: '2026-10-09',
        incompatibleArea: false,
        limitations: ['Independent of IRR.'],
      },
      advertisedRent: null,
    };
    request.flush(payload);
    await pending;
    fixture.detectChanges();
    expect(workspace.properties()[0].purchasePrice).toBe(12_000_000);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Not a valuation.');
  });

  it('does not prefill when the user cancels confirmation', async () => {
    spyOn(window, 'confirm').and.returnValue(false);
    const fixture = TestBed.createComponent(MarketComponent);
    await fixture.componentInstance.useInComparison({
      id: '11111111-1111-4111-8111-111111111111',
      locality: 'Whitefield',
      builderProject: 'Tower',
      propertyType: 'Apartment',
      configuration: '2 BHK',
      recordType: 'asking_price',
      verificationStatus: 'source_cited',
      amount: 12_000_000,
      areaValue: 1200,
      areaType: 'super_built_up',
      areaUnit: 'sq_ft',
      pricePerSqFt: 10_000,
      monthlyRentPerSqFt: null,
      sourceName: 'Synthetic fixture — not a live listing',
      sourceUrl: null,
      sourceRecordId: 'SYN',
      observedOn: '2026-10-01',
      importedUtc: '2026-10-25T00:00:00Z',
      qualityNotes: null,
      licenceOrUsageNotes: null,
      isSyntheticTestData: true,
      isOutlier: false,
      isStale: false,
      isUnverified: false,
      hasCompleteSource: true,
      providerKey: 'csv_file',
    });
    expect(workspace.properties()).toHaveSize(0);
  });
});

function flushProviders(http: HttpTestingController, base: string): void {
  http.expectOne(`${base}/api/market-providers`).flush(providerStatus());
}

function flushSearch(http: HttpTestingController, search: MarketSearchResponse, stats: MarketStatsResponse): void {
  const requests = http.match(() => true);
  expect(requests.length).toBe(2);
  for (const request of requests) {
    if (request.request.url.includes('/stats')) {
      request.flush(stats);
    } else {
      request.flush(search);
    }
  }
}

function emptySearch(): MarketSearchResponse {
  return {
    items: [],
    count: 0,
    totalCount: 0,
    skip: 0,
    take: 20,
    oldestObservedOn: null,
    newestObservedOn: null,
    isSparse: true,
    containsStale: false,
    containsUnverified: false,
    containsSynthetic: false,
    onlySyntheticAvailable: true,
  };
}

function syntheticStats(): MarketStatsResponse {
  return {
    locality: 'Whitefield',
    propertyType: 'Apartment',
    configuration: null,
    includeSynthetic: true,
    onlySyntheticAvailable: true,
    usingSyntheticTestData: true,
    genuineCount: 0,
    syntheticCount: 10,
    buckets: [],
    history: {
      interval: 'month',
      sufficient: false,
      usingSyntheticTestData: true,
      reason: 'Insufficient history: need at least 3 months with 3 comparable observations each.',
      points: [],
    },
    notes: 'synthetic test observations',
  };
}

function providerStatus(): MarketProviderStatusResponse {
  return {
    hasGenuineLiveFeed: false,
    allowManualRefresh: true,
    ingestionPolicy: 'Insert-only.',
    genuineObservationCount: 0,
    syntheticObservationCount: 10,
    newestObservationDate: null,
    lastSuccessfulImportUtc: null,
    lastImport: null,
    providers: [
      {
        key: 'csv_file',
        displayName: 'CSV import',
        kind: 'file',
        fetchSupported: false,
        configured: true,
        liveFeed: false,
        coverage: 'Synthetic fixture',
        accessNotes: 'Not a live feed',
        documentationUrl: '/api/market-observations/template',
      },
    ],
    notes: 'ready for provider integration but currently has no genuine live market feed',
  };
}
