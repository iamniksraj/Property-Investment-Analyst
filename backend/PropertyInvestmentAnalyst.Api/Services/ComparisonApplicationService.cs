using Microsoft.EntityFrameworkCore;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class ComparisonApplicationService(AppDbContext db)
{
    public async Task<IReadOnlyList<ComparisonSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var analyses = await db.ComparisonAnalyses
            .AsNoTracking()
            .Include(item => item.Properties)
            .OrderByDescending(item => item.UpdatedUtc)
            .ToListAsync(cancellationToken);

        return analyses.Select(ComparisonMapper.ToSummary).ToArray();
    }

    public async Task<ComparisonDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await LoadAsync(id, tracking: false, cancellationToken);
        return ComparisonMapper.ToDetail(analysis);
    }

    public async Task<ComparisonDetailDto> CreateAsync(SaveComparisonRequest request, CancellationToken cancellationToken)
    {
        ComparisonValidator.Validate(request);

        var now = DateTimeOffset.UtcNow;
        var analysis = new ComparisonAnalysis
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = TrimDescription(request.Description),
            CreatedUtc = now,
            UpdatedUtc = now,
            SchemaVersion = ComparisonAnalysis.CurrentSchemaVersion,
        };

        for (var index = 0; index < request.Properties.Count; index += 1)
        {
            analysis.Properties.Add(ComparisonMapper.ToEntity(request.Properties[index], analysis.Id, index));
        }

        db.ComparisonAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
        return ComparisonMapper.ToDetail(analysis);
    }

    public async Task<ComparisonDetailDto> UpdateAsync(Guid id, SaveComparisonRequest request, CancellationToken cancellationToken)
    {
        ComparisonValidator.Validate(request);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.ComparisonAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ComparisonNotFoundException(id);

        if (request.ExpectedUpdatedUtc is { } expected && existing.UpdatedUtc != expected)
        {
            throw new ComparisonConflictException();
        }

        var existingPropertyIds = await db.ComparedProperties
            .AsNoTracking()
            .Where(item => item.ComparisonAnalysisId == id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var incomingIdList = request.Properties
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToList();
        if (incomingIdList.Count != incomingIdList.Distinct().Count())
        {
            throw new ComparisonValidationException(new Dictionary<string, string[]>
            {
                ["properties"] = ["Each saved property id can appear only once."],
            });
        }

        foreach (var incomingId in incomingIdList)
        {
            if (!existingPropertyIds.Contains(incomingId))
            {
                throw new ComparisonValidationException(new Dictionary<string, string[]>
                {
                    ["properties"] = [$"Property '{incomingId}' does not belong to this comparison."],
                });
            }
        }

        var name = request.Name.Trim();
        var description = TrimDescription(request.Description);
        var updatedUtc = DateTimeOffset.UtcNow;

        await db.ComparedProperties
            .Where(item => item.ComparisonAnalysisId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await db.ComparisonAnalyses
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Name, name)
                    .SetProperty(item => item.Description, description)
                    .SetProperty(item => item.UpdatedUtc, updatedUtc)
                    .SetProperty(item => item.SchemaVersion, ComparisonAnalysis.CurrentSchemaVersion),
                cancellationToken);

        db.ChangeTracker.Clear();

        for (var index = 0; index < request.Properties.Count; index += 1)
        {
            db.ComparedProperties.Add(ComparisonMapper.ToEntity(request.Properties[index], id, index));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await db.ComparisonAnalyses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (analysis is null)
        {
            throw new ComparisonNotFoundException(id);
        }

        db.ComparisonAnalyses.Remove(analysis);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ComparisonAnalysis> LoadAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = db.ComparisonAnalyses
            .Include(item => item.Properties)
            .ThenInclude(item => item.Scenarios)
            .Where(item => item.Id == id);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken) ?? throw new ComparisonNotFoundException(id);
    }

    private static string? TrimDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
