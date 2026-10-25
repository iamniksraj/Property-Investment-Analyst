import { toAnalystProperties } from './analyst-context';
import { createIllustrativeSampleProperties, buildComparisonView } from './comparison-engine';

describe('analyst-context', () => {
  it('maps TypeScript engine results without inventing a composite score', () => {
    const view = buildComparisonView(createIllustrativeSampleProperties());
    const payload = toAnalystProperties(view.rows);
    expect(payload.length).toBe(2);
    expect(payload[0].snapshot?.grossAnnualRentalYieldPercent).toBeTruthy();
    expect(payload[0].mortgage?.annualCashFlowAfterOperatingExpensesAndLoanPayments).toBeDefined();
    expect(payload[0].base?.netProfit).toBeDefined();
    expect(payload[0].base?.grossPropertyAppreciationPercent).toBeDefined();
    expect(JSON.stringify(payload)).not.toContain('bestInvestment');
  });
});
