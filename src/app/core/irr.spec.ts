import { calculateIrr } from './irr';

describe('calculateIrr', () => {
  it('matches a closed-form one-period 10% example', () => {
    // -1,000 at t=0 and +1,100 at t=1 has IRR = 10% by definition.
    const result = calculateIrr([-1_000, 1_100]);
    expect(result.rate).toBeCloseTo(0.1, 10);
    expect(result.reason).toBeNull();
  });

  it('matches a two-year 10% example calculated independently of the solver', () => {
    // NPV(10%) = -1,200,000 + 120,000/1.1 + 1,320,000/1.21 = 0.
    const result = calculateIrr([-1_200_000, 120_000, 1_320_000]);
    expect(result.rate).toBeCloseTo(0.1, 8);
  });

  it('returns not available when there is no sign change', () => {
    expect(calculateIrr([100, 50, 25]).rate).toBeNull();
    expect(calculateIrr([-100, -50]).rate).toBeNull();
    expect(calculateIrr([0, 0, 0]).rate).toBeNull();
    expect(calculateIrr([-100]).rate).toBeNull();
    expect(calculateIrr([]).rate).toBeNull();
  });

  it('never returns NaN or Infinity', () => {
    const samples = [[-1, 2], [1, -2, 3], [-100, 0, 0, 0], [Number.NaN, 1]];
    for (const cashFlows of samples) {
      const result = calculateIrr(cashFlows);
      if (result.rate !== null) {
        expect(Number.isFinite(result.rate)).toBeTrue();
      }
    }
  });
});
