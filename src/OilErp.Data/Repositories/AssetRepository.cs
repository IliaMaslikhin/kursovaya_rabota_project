using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for Asset entities using Dapper
/// </summary>
public class AssetRepository : BaseRepository<Asset, string>, IAssetRepository
{
    public AssetRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Asset?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            WHERE id = @id";

        return await QuerySingleOrDefaultAsync<Asset>(sql, new { id }, cancellationToken);
    }

    public async Task<Asset?> GetByTagNumberAsync(string tagNumber, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            WHERE tag_number = @tagNumber";

        return await QuerySingleOrDefaultAsync<Asset>(sql, new { tagNumber }, cancellationToken);
    }

    public override async Task<IEnumerable<Asset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            ORDER BY tag_number";

        return await QueryAsync<Asset>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Asset>> GetByPlantCodeAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            WHERE plant_code = @plantCode
            ORDER BY tag_number";

        return await QueryAsync<Asset>(sql, new { plantCode }, cancellationToken);
    }

    public override async Task<string> CreateAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO assets.global_assets (id, tag_number, description, plant_code, asset_type, created_at, updated_at)
            VALUES (@Id, @TagNumber, @Description, @PlantCode, @AssetType, @CreatedAt, @UpdatedAt)";

        await ExecuteAsync(sql, asset, cancellationToken);
        return asset.Id;
    }

    public override async Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE assets.global_assets 
            SET tag_number = @TagNumber, description = @Description, plant_code = @PlantCode, 
                asset_type = @AssetType, updated_at = @UpdatedAt
            WHERE id = @Id";

        await ExecuteAsync(sql, asset, cancellationToken);
    }

    public override async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM assets.global_assets WHERE id = @id";
        await ExecuteAsync(sql, new { id }, cancellationToken);
    }

    // Additional methods required by IAssetRepository
    public async Task<IEnumerable<Asset>> GetByAssetTypeAsync(string assetType, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            WHERE asset_type = @assetType
            ORDER BY tag_number";

        return await QueryAsync<Asset>(sql, new { assetType }, cancellationToken);
    }

    public async Task<IEnumerable<Asset>> GetAssetsWithCriticalDefectsAsync(CancellationToken cancellationToken = default)
    {
        // This would require joining with defects table - placeholder implementation
        const string sql = @"
            SELECT DISTINCT a.id, a.tag_number as TagNumber, a.description, a.plant_code as PlantCode, 
                   a.asset_type as AssetType, a.created_at as CreatedAt, a.updated_at as UpdatedAt
            FROM assets.global_assets a
            ORDER BY a.tag_number";

        return await QueryAsync<Asset>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Asset>> GetAssetsWithOverdueWorkOrdersAsync(CancellationToken cancellationToken = default)
    {
        // This would require joining with work orders table - placeholder implementation
        const string sql = @"
            SELECT DISTINCT a.id, a.tag_number as TagNumber, a.description, a.plant_code as PlantCode, 
                   a.asset_type as AssetType, a.created_at as CreatedAt, a.updated_at as UpdatedAt
            FROM assets.global_assets a
            ORDER BY a.tag_number";

        return await QueryAsync<Asset>(sql, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Asset>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, tag_number as TagNumber, description, plant_code as PlantCode, 
                   asset_type as AssetType, created_at as CreatedAt, updated_at as UpdatedAt
            FROM assets.global_assets 
            WHERE tag_number ILIKE @searchTerm OR description ILIKE @searchTerm
            ORDER BY tag_number";

        var searchPattern = $"%{searchTerm}%";
        return await QueryAsync<Asset>(sql, new { searchTerm = searchPattern }, cancellationToken);
    }

    // Placeholder implementations for methods requiring complex queries
    public Task<Asset?> GetWithSegmentsAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public Task<Asset?> GetWithDefectsAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public Task<Asset?> GetWithWorkOrdersAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public Task<Asset?> GetWithAllRelatedDataAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    public async Task<int> GetCountByPlantAsync(string plantCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM assets.global_assets WHERE plant_code = @plantCode";
        var result = await ExecuteScalarAsync<int>(sql, new { plantCode }, cancellationToken);
        return result;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM assets.global_assets";
        var result = await ExecuteScalarAsync<int>(sql, cancellationToken: cancellationToken);
        return result;
    }
}