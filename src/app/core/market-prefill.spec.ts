import { draftFromObservation } from './market-prefill';
import { MarketObservationDto } from './market-api.models';

function observation(overrides: Partial<MarketObservationDto> = {}): MarketObservationDto {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    locality: 'Whitefield',
    builderProject: 'Synthetic fixture tower A',
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
    sourceRecordId: 'SYN-WF-ASK-003',
    observedOn: '2026-10-03',
    importedUtc: '2026-10-25T00:00:00Z',
    qualityNotes: 'Test fixture.',
    licenceOrUsageNotes: 'Test data only.',
    isSyntheticTestData: true,
    isOutlier: false,
    isStale: false,
    isUnverified: false,
    hasCompleteSource: true,
    providerKey: 'csv_file',
    ...overrides,
  };
}

describe('draftFromObservation', () => {
  it('copies asking price and super built-up area', () => {
    const { draft, warnings } = draftFromObservation(observation());
    expect(draft.purchasePrice).toBe(12_000_000);
    expect(draft.superBuiltUpAreaSqFt).toBe(1200);
    expect(draft.monthlyRent).toBeNull();
    expect(warnings.some((item) => item.includes('synthetic'))).toBeTrue();
  });

  it('does not treat advertised rent as a purchase price or copy carpet area as super built-up', () => {
    const { draft, warnings } = draftFromObservation(
      observation({
        recordType: 'advertised_rent',
        amount: 40_000,
        areaType: 'carpet',
        areaValue: 900,
        isSyntheticTestData: false,
      }),
    );
    expect(draft.purchasePrice).toBeNull();
    expect(draft.monthlyRent).toBe(40_000);
    expect(draft.superBuiltUpAreaSqFt).toBeNull();
    expect(warnings.some((item) => item.includes('advertised rent'))).toBeTrue();
    expect(warnings.some((item) => item.includes('carpet'))).toBeTrue();
  });
});
