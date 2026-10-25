import { ComparisonPropertyDraft } from './comparison-models';
import { createEmptyPropertyDraft } from './comparison-engine';
import { MarketObservationDto } from './market-api.models';

export function draftFromObservation(observation: MarketObservationDto): {
  draft: ComparisonPropertyDraft;
  warnings: string[];
} {
  const warnings: string[] = [];
  const draft = createEmptyPropertyDraft('pending');
  draft.name = `${observation.configuration} · ${observation.builderProject}`.trim();
  draft.locality = observation.locality;
  draft.builderProject = observation.builderProject;
  draft.propertyType = observation.propertyType;

  if (observation.recordType === 'asking_price' || observation.recordType === 'verified_transaction') {
    draft.purchasePrice = observation.amount;
  } else if (observation.recordType === 'advertised_rent') {
    draft.monthlyRent = observation.amount;
    warnings.push('This record is advertised rent, not a sale price. Purchase price was left blank.');
  } else {
    warnings.push('This record type does not map to purchase price or rent. Review the amounts before calculating.');
  }

  if (observation.areaType === 'super_built_up' && observation.areaUnit === 'sq_ft' && observation.areaValue) {
    draft.superBuiltUpAreaSqFt = observation.areaValue;
  } else {
    warnings.push(
      `Area was not copied because it is ${observation.areaType.split('_').join(' ')} in ${observation.areaUnit.split('_').join(' ')}, not super built-up square feet.`,
    );
  }

  if (observation.isSyntheticTestData) {
    warnings.push('This observation is labelled synthetic test data. It is not a live Bengaluru listing.');
  }
  if (observation.isStale) {
    warnings.push('The observation date is more than 180 days old.');
  }
  if (observation.isUnverified) {
    warnings.push('Verification status is unverified.');
  }
  if (observation.isOutlier) {
    warnings.push('This amount was flagged as an outlier and should be treated with extra caution.');
  }

  return { draft, warnings };
}
