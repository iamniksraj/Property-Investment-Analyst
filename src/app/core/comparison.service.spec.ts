import { TestBed } from '@angular/core/testing';
import { ComparisonWorkspaceService } from './comparison.service';

describe('ComparisonWorkspaceService', () => {
  let service: ComparisonWorkspaceService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ComparisonWorkspaceService);
  });

  it('adds, edits and removes properties without exceeding three', () => {
    expect(service.addEmpty()?.id).toBe('property-1');
    expect(service.addEmpty()?.id).toBe('property-2');
    expect(service.addEmpty()?.id).toBe('property-3');
    expect(service.addEmpty()).toBeNull();
    expect(service.properties().length).toBe(3);

    const updated = { ...service.properties()[1], name: 'Edited' };
    service.update(updated.id, updated);
    expect(service.properties()[1].name).toBe('Edited');
    expect(service.properties()[0].name).toBe('');

    service.remove('property-2');
    expect(service.properties().map((item) => item.id)).toEqual(['property-1', 'property-3']);
    expect(service.properties()[1].name).toBe('');
  });

  it('loads labelled illustrative samples independently of the dashboard', () => {
    service.loadIllustrativeSamples();
    expect(service.properties()).toHaveSize(2);
    expect(service.view().rows.every((row) => row.ready)).toBeTrue();
    expect(service.properties()[0].name).toBe('Whitefield 2 BHK');
    service.properties()[0].monthlyRent = 1;
    service.loadIllustrativeSamples();
    expect(service.properties()[0].monthlyRent).toBe(40_000);
    expect(service.dirty()).toBeTrue();
    expect(service.meta().savedId).toBeNull();
  });

  it('applies a saved analysis and then tracks later edits as dirty', () => {
    service.applySaved({
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      name: 'Saved desk',
      description: 'Persisted',
      createdUtc: '2026-01-01T00:00:00Z',
      updatedUtc: '2026-01-02T00:00:00Z',
      schemaVersion: 1,
      properties: [
        {
          id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
          name: 'Whitefield 2 BHK',
          locality: 'Whitefield',
          builderProject: 'Sample',
          propertyType: 'Apartment',
          purchasePrice: 12_000_000,
          superBuiltUpAreaSqFt: 1200,
          monthlyRent: 40_000,
          monthlyMaintenance: 5000,
          downPaymentPercent: 20,
          annualInterestRatePercent: 8.5,
          loanTenureYears: 20,
          acquisitionCostPercent: 7,
          holdingPeriodYears: 10,
          vacancyPercent: 5,
          annualPropertyTax: 0,
          annualRepairAndMaintenance: 0,
          conservative: {
            propertyAppreciationPercent: 2,
            rentGrowthPercent: 2,
            vacancyPercent: 8,
            operatingExpenseGrowthPercent: 6,
            sellingCostPercent: 2,
          },
          base: {
            propertyAppreciationPercent: 5,
            rentGrowthPercent: 4,
            vacancyPercent: 5,
            operatingExpenseGrowthPercent: 5,
            sellingCostPercent: 2,
          },
          optimistic: {
            propertyAppreciationPercent: 8,
            rentGrowthPercent: 6,
            vacancyPercent: 3,
            operatingExpenseGrowthPercent: 4,
            sellingCostPercent: 2,
          },
        },
      ],
    });

    expect(service.dirty()).toBeFalse();
    expect(service.meta().savedId).toBe('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa');
    expect(service.properties()[0].scenarios.base.propertyAppreciationPercent).toBe(5);

    service.update(service.properties()[0].id, {
      ...service.properties()[0],
      monthlyRent: 41_000,
    });
    expect(service.dirty()).toBeTrue();
  });

  it('adds an observation as a new property without replacing existing ones', () => {
    service.addEmpty();
    const added = service.addFromObservation(
      {
        ...service.properties()[0],
        name: 'From market',
        locality: 'Whitefield',
        purchasePrice: 12_000_000,
      },
      'Prefill notice',
    );
    expect(service.properties()).toHaveSize(2);
    expect(service.properties()[0].name).toBe('');
    expect(added?.name).toBe('From market');
    expect(service.consumePrefillNotice()).toBe('Prefill notice');
    expect(service.consumePrefillNotice()).toBeNull();
  });
});
