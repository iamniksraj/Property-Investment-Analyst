using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services;

namespace PropertyInvestmentAnalyst.Api.Tests;

public class ComparisonValidatorTests
{
    [Fact]
    public void Rejects_missing_name_and_too_many_properties()
    {
        var request = RequestFactory.ValidRequest();
        request.Name = " ";
        request.Properties.Add(RequestFactory.ValidProperty("Two"));
        request.Properties.Add(RequestFactory.ValidProperty("Three"));
        request.Properties.Add(RequestFactory.ValidProperty("Four"));

        var error = Assert.Throws<ComparisonValidationException>(() => ComparisonValidator.Validate(request));
        Assert.Contains("name", error.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("properties", error.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_invalid_rates_and_negative_price()
    {
        var request = RequestFactory.ValidRequest();
        request.Properties[0].PurchasePrice = -1;
        request.Properties[0].DownPaymentPercent = 150;
        request.Properties[0].LoanTenureYears = 0;
        request.Properties[0].Conservative = null;

        var error = Assert.Throws<ComparisonValidationException>(() => ComparisonValidator.Validate(request));
        Assert.NotEmpty(error.Errors);
        Assert.Contains(error.Errors.Keys, key => key.Contains("purchasePrice", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(error.Errors.Keys, key => key.Contains("downPaymentPercent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(error.Errors.Keys, key => key.Contains("loanTenureYears", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(error.Errors.Keys, key => key.Contains("conservative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Accepts_a_complete_two_property_request()
    {
        var request = RequestFactory.ValidRequest();
        request.Properties.Add(RequestFactory.ValidProperty("HSR"));
        ComparisonValidator.Validate(request);
    }
}
