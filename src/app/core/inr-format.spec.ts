import { formatInr, formatInrCompact, formatIrr, formatPercent } from './inr-format';

describe('INR formatting', () => {
  it('uses Indian digit grouping for rupees', () => {
    expect(formatInr(12_00_000).replace(/\s/g, '')).toBe('₹12,00,000');
    expect(formatInr(1_20_00_000).replace(/\s/g, '')).toBe('₹1,20,00,000');
  });

  it('formats fractional rupees when requested', () => {
    expect(formatInr(8500.5, 2)).toBe('₹8,500.50');
  });

  it('formats percentages to two decimal places', () => {
    expect(formatPercent(4)).toBe('4.00%');
    expect(formatPercent(3.456)).toBe('3.46%');
  });

  it('does not print NaN or Infinity', () => {
    expect(formatInr(Number.NaN)).toBe('—');
    expect(formatInr(Number.POSITIVE_INFINITY)).toBe('—');
    expect(formatPercent(Number.NEGATIVE_INFINITY)).toBe('—');
  });

  it('labels undefined IRR as not available', () => {
    expect(formatIrr(null)).toBe('Not available');
    expect(formatIrr(Number.NaN)).toBe('Not available');
    expect(formatIrr(0.1)).toBe('10.00%');
  });

  it('summarises large amounts in lakh and crore units', () => {
    expect(formatInrCompact(1_20_00_000)).toBe('₹1.20 crore');
    expect(formatInrCompact(12_00_000)).toBe('₹12.00 lakh');
    expect(formatInrCompact(-2_50_00_000)).toBe('-₹2.50 crore');
    expect(formatInrCompact(Number.NaN)).toBe('—');
  });
});
