namespace PropertyInvestmentAnalyst.Api.Tests;

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<ApiFactory>;
