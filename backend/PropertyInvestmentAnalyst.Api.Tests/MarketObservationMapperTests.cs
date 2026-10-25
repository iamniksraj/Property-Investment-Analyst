using PropertyInvestmentAnalyst.Api.Domain;
using PropertyInvestmentAnalyst.Api.Services;

namespace PropertyInvestmentAnalyst.Api.Tests;

public class MarketObservationMapperTests
{
    [Fact]
    public void Median_uses_the_middle_value_and_does_not_mix_even_pairs_as_a_mean_of_all()
    {
        Assert.Equal(3m, MarketObservationMapper.Median([1, 3, 9]));
        Assert.Equal(2.5m, MarketObservationMapper.Median([1, 2, 3, 4]));
        Assert.Null(MarketObservationMapper.Median([]));
    }

    [Fact]
    public void Price_per_sq_ft_requires_super_built_up_square_feet()
    {
        var request = RequestFactory.ValidMarketObservation();
        request.RecordType = MarketObservation.RecordTypeAskingPrice;
        request.Amount = 12_000_000;
        request.AreaValue = 1200;
        request.AreaType = MarketObservation.AreaSuperBuiltUp;
        request.AreaUnit = MarketObservation.UnitSqFt;
        var sale = MarketObservationMapper.ToEntity(request, DateTimeOffset.UtcNow);
        Assert.Equal(10_000m, sale.PricePerSqFt);
        Assert.Null(sale.MonthlyRentPerSqFt);

        request.AreaType = MarketObservation.AreaCarpet;
        var carpet = MarketObservationMapper.ToEntity(request, DateTimeOffset.UtcNow);
        Assert.Null(carpet.PricePerSqFt);

        request.AreaType = MarketObservation.AreaSuperBuiltUp;
        request.RecordType = MarketObservation.RecordTypeAdvertisedRent;
        request.Amount = 40_000;
        var rent = MarketObservationMapper.ToEntity(request, DateTimeOffset.UtcNow);
        Assert.Null(rent.PricePerSqFt);
        Assert.Equal(33.33m, rent.MonthlyRentPerSqFt);
    }

    [Fact]
    public void Duplicate_keys_ignore_case_and_preserve_observation_date()
    {
        var first = MarketObservationMapper.DuplicateKey(
            "Source", "A1", "Whitefield", "Tower", "2 BHK", "asking_price", 100, new DateOnly(2026, 10, 1));
        var same = MarketObservationMapper.DuplicateKey(
            "source", "a1", "whitefield", "tower", "2 bhk", "asking_price", 100, new DateOnly(2026, 10, 1));
        var later = MarketObservationMapper.DuplicateKey(
            "Source", "A1", "Whitefield", "Tower", "2 BHK", "asking_price", 100, new DateOnly(2026, 10, 2));
        Assert.Equal(first, same);
        Assert.NotEqual(first, later);
    }

    [Fact]
    public void Iqr_flags_extreme_amounts_only_when_the_sample_is_large_enough()
    {
        var peers = new decimal[] { 11_000_000, 11_500_000, 12_000_000, 12_200_000, 12_500_000, 12_800_000, 13_000_000 };
        Assert.False(MarketObservationMapper.IsOutlierAmount(12_200_000, peers));
        Assert.True(MarketObservationMapper.IsOutlierAmount(90_000_000, peers));
        Assert.False(MarketObservationMapper.IsOutlierAmount(90_000_000, [11_000_000, 12_000_000]));
    }
}
