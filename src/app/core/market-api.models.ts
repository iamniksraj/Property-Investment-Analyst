export interface MarketObservationDto {
  id: string;
  locality: string;
  builderProject: string;
  propertyType: string;
  configuration: string;
  recordType: string;
  verificationStatus: string;
  amount: number;
  areaValue: number | null;
  areaType: string;
  areaUnit: string;
  pricePerSqFt: number | null;
  monthlyRentPerSqFt: number | null;
  sourceName: string;
  sourceUrl: string | null;
  sourceRecordId: string | null;
  observedOn: string;
  importedUtc: string;
  qualityNotes: string | null;
  licenceOrUsageNotes: string | null;
  isSyntheticTestData: boolean;
  isOutlier: boolean;
  isStale: boolean;
  isUnverified: boolean;
  hasCompleteSource: boolean;
  providerKey: string | null;
}

export interface MarketSearchResponse {
  items: MarketObservationDto[];
  count: number;
  totalCount: number;
  skip: number;
  take: number;
  oldestObservedOn: string | null;
  newestObservedOn: string | null;
  isSparse: boolean;
  containsStale: boolean;
  containsUnverified: boolean;
  containsSynthetic: boolean;
  onlySyntheticAvailable: boolean;
}

export interface ExclusionCountDto {
  reason: string;
  count: number;
  explanation: string;
}

export interface EvidenceQualityDto {
  sampleSize: string;
  recency: string;
  verification: string;
  areaComparability: string;
  sourceCompleteness: string;
  synthetic: string;
  outliers: string;
  notes: string[];
}

export interface HistoryPointDto {
  yearMonth: string;
  count: number;
  medianAmount: number | null;
  insufficientMonth: boolean;
}

export interface HistorySeriesDto {
  interval: string;
  sufficient: boolean;
  usingSyntheticTestData: boolean;
  reason: string;
  points: HistoryPointDto[];
}

export interface MarketStatisticBucketDto {
  recordType: string;
  matchedCount: number;
  sampleSize: number;
  excludedOutliers: number;
  isSparse: boolean;
  insufficientData: boolean;
  mixedAreaDefinitions: boolean;
  oldestObservedOn: string | null;
  newestObservedOn: string | null;
  minAmount: number | null;
  maxAmount: number | null;
  medianAmount: number | null;
  percentile25: number | null;
  percentile75: number | null;
  medianPricePerSqFt: number | null;
  medianMonthlyRentPerSqFt: number | null;
  exclusions: ExclusionCountDto[];
  quality: EvidenceQualityDto;
  label: string;
  methodology: string;
}

export interface MarketStatsResponse {
  locality: string;
  propertyType: string | null;
  configuration: string | null;
  includeSynthetic: boolean;
  onlySyntheticAvailable: boolean;
  usingSyntheticTestData: boolean;
  genuineCount: number;
  syntheticCount: number;
  buckets: MarketStatisticBucketDto[];
  history: HistorySeriesDto;
  notes: string;
}

export interface AssumptionComparisonRequest {
  locality: string;
  propertyType?: string | null;
  configuration?: string | null;
  includeSynthetic: boolean;
  acknowledgeIncompatibleArea: boolean;
  purchasePrice?: number | null;
  monthlyRent?: number | null;
  areaValue?: number | null;
  areaType?: string | null;
  areaUnit?: string | null;
}

export interface AssumptionMetricDto {
  recordType: string;
  compared: boolean;
  status: string;
  comparableCount: number;
  userAmount: number | null;
  userPerSqFt: number | null;
  medianAmount: number | null;
  percentile25: number | null;
  percentile75: number | null;
  medianPerSqFt: number | null;
  amountDifference: number | null;
  amountDifferencePercent: number | null;
  perSqFtDifference: number | null;
  perSqFtDifferencePercent: number | null;
  oldestObservedOn: string | null;
  newestObservedOn: string | null;
  incompatibleArea: boolean;
  limitations: string[];
}

export interface AssumptionComparisonResponse {
  usingSyntheticTestData: boolean;
  disclaimer: string;
  askingPrice: AssumptionMetricDto | null;
  advertisedRent: AssumptionMetricDto | null;
}

export interface MarketImportRowResult {
  lineNumber: number;
  status: string;
  errors: string[];
  duplicateKey?: string | null;
  observation?: MarketObservationDto | null;
}

export interface MarketImportResult {
  totalRows: number;
  valid: number;
  invalid: number;
  duplicates: number;
  outliers: number;
  inserted: number;
  updated?: number;
  skipped?: number;
  rows: MarketImportRowResult[];
}

export interface MarketProviderDescriptor {
  key: string;
  displayName: string;
  kind: string;
  fetchSupported: boolean;
  configured: boolean;
  liveFeed: boolean;
  coverage: string;
  accessNotes: string;
  documentationUrl: string;
}

export interface MarketImportRunDto {
  providerKey: string;
  status: string;
  startedUtc: string;
  completedUtc: string;
  observedFrom: string | null;
  observedTo: string | null;
  inserted: number;
  updated: number;
  skipped: number;
  duplicates: number;
  invalid: number;
  errorCount: number;
  summary: string;
}

export interface MarketProviderStatusResponse {
  hasGenuineLiveFeed: boolean;
  allowManualRefresh: boolean;
  ingestionPolicy: string;
  genuineObservationCount: number;
  syntheticObservationCount: number;
  newestObservationDate: string | null;
  lastSuccessfulImportUtc: string | null;
  lastImport: MarketImportRunDto | null;
  providers: MarketProviderDescriptor[];
  notes: string;
}

export const RECORD_TYPE_LABELS: Record<string, string> = {
  asking_price: 'Asking price',
  advertised_rent: 'Advertised rent',
  verified_transaction: 'Verified transaction',
  other: 'Other',
};

export const AREA_TYPE_LABELS: Record<string, string> = {
  super_built_up: 'Super built-up',
  built_up: 'Built-up',
  carpet: 'Carpet',
  plot: 'Plot',
  unspecified: 'Unspecified',
};
