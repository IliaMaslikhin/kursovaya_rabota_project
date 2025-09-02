using System.Data;
using Dapper;
using OilErp.Domain.Entities;
using OilErp.Domain.Interfaces;

namespace OilErp.Data.Repositories;

/// <summary>
/// Repository implementation for Material catalog entities
/// </summary>
public class MaterialRepository : BaseRepository<Material, string>, IMaterialRepository
{
    public MaterialRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Material?> GetByIdAsync(string code, CancellationToken cancellationToken = default)
    {
        return await GetByCodeAsync(code, cancellationToken);
    }

    public async Task<Material?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        const string sql = @"
            SELECT m.code, m.name, m.density, m.type, m.specification, m.created_at as CreatedAt
            FROM catalogs.materials m 
            WHERE m.code = @Code";

        return await QuerySingleOrDefaultAsync<Material>(sql, new { Code = code }, cancellationToken);
    }

    public override async Task<IEnumerable<Material>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT m.code, m.name, m.density, m.type, m.specification, m.created_at as CreatedAt
            FROM catalogs.materials m 
            ORDER BY m.name";

        return await QueryAsync<Material>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<string> CreateAsync(Material material, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        // Check if material code already exists
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM catalogs.materials 
            WHERE code = @Code";

        var codeExists = await ExecuteScalarAsync<int>(checkSql, new { material.Code }, cancellationToken) > 0;
        
        if (codeExists)
        {
            throw new InvalidOperationException($"Material with code '{material.Code}' already exists");
        }

        const string sql = @"
            INSERT INTO catalogs.materials (code, name, density, type, specification, created_at)
            VALUES (@Code, @Name, @Density, @Type, @Specification, @CreatedAt)
            RETURNING code";

        material.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            material.Code,
            material.Name,
            material.Density,
            material.Type,
            material.Specification,
            material.CreatedAt
        };

        var result = await ExecuteScalarAsync<string>(sql, parameters, cancellationToken);
        return result ?? material.Code;
    }

    public override async Task UpdateAsync(Material material, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        const string sql = @"
            UPDATE catalogs.materials 
            SET name = @Name,
                density = @Density,
                type = @Type,
                specification = @Specification
            WHERE code = @Code";

        var parameters = new
        {
            material.Code,
            material.Name,
            material.Density,
            material.Type,
            material.Specification
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Material with code '{material.Code}' not found for update");
        }
    }

    public override async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // Check if material is used in segments
        const string checkUsageSql = @"
            SELECT COUNT(*) 
            FROM segments 
            WHERE material_code = @Code";

        var usageCount = await ExecuteScalarAsync<int>(checkUsageSql, new { Code = code }, cancellationToken);
        
        if (usageCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete material '{code}' as it is used in {usageCount} segments");
        }

        const string sql = "DELETE FROM catalogs.materials WHERE code = @Code";
        
        var affectedRows = await ExecuteAsync(sql, new { Code = code }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Material with code '{code}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Material>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        const string sql = @"
            SELECT m.code, m.name, m.density, m.type, m.specification, m.created_at as CreatedAt
            FROM catalogs.materials m 
            WHERE LOWER(m.name) LIKE LOWER(@SearchTerm) OR LOWER(m.code) LIKE LOWER(@SearchTerm)
            ORDER BY m.name";

        return await QueryAsync<Material>(sql, new { SearchTerm = $"%{searchTerm}%" }, cancellationToken);
    }

    public async Task<IEnumerable<Material>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        const string sql = @"
            SELECT m.code, m.name, m.density, m.type, m.specification, m.created_at as CreatedAt
            FROM catalogs.materials m 
            WHERE m.type = @Type
            ORDER BY m.name";

        return await QueryAsync<Material>(sql, new { Type = type }, cancellationToken);
    }

    public async Task<IEnumerable<Material>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        return await SearchByNameAsync(searchTerm, cancellationToken);
    }
}

/// <summary>
/// Repository implementation for Coating catalog entities
/// </summary>
public class CoatingRepository : BaseRepository<Coating, string>, ICoatingRepository
{
    public CoatingRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Coating?> GetByIdAsync(string code, CancellationToken cancellationToken = default)
    {
        return await GetByCodeAsync(code, cancellationToken);
    }

    public async Task<Coating?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        const string sql = @"
            SELECT c.code, c.name, c.type, c.manufacturer, c.specification, c.created_at as CreatedAt
            FROM catalogs.coatings c 
            WHERE c.code = @Code";

        return await QuerySingleOrDefaultAsync<Coating>(sql, new { Code = code }, cancellationToken);
    }

    public override async Task<IEnumerable<Coating>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT c.code, c.name, c.type, c.manufacturer, c.specification, c.created_at as CreatedAt
            FROM catalogs.coatings c 
            ORDER BY c.name";

        return await QueryAsync<Coating>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<string> CreateAsync(Coating coating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coating);

        // Check if coating code already exists
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM catalogs.coatings 
            WHERE code = @Code";

        var codeExists = await ExecuteScalarAsync<int>(checkSql, new { coating.Code }, cancellationToken) > 0;
        
        if (codeExists)
        {
            throw new InvalidOperationException($"Coating with code '{coating.Code}' already exists");
        }

        const string sql = @"
            INSERT INTO catalogs.coatings (code, name, type, manufacturer, specification, created_at)
            VALUES (@Code, @Name, @Type, @Manufacturer, @Specification, @CreatedAt)
            RETURNING code";

        coating.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            coating.Code,
            coating.Name,
            coating.Type,
            coating.Manufacturer,
            coating.Specification,
            coating.CreatedAt
        };

        var result = await ExecuteScalarAsync<string>(sql, parameters, cancellationToken);
        return result ?? coating.Code;
    }

    public override async Task UpdateAsync(Coating coating, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coating);

        const string sql = @"
            UPDATE catalogs.coatings 
            SET name = @Name,
                type = @Type,
                manufacturer = @Manufacturer,
                specification = @Specification
            WHERE code = @Code";

        var parameters = new
        {
            coating.Code,
            coating.Name,
            coating.Type,
            coating.Manufacturer,
            coating.Specification
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Coating with code '{coating.Code}' not found for update");
        }
    }

    public override async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // Check if coating is used in segments
        const string checkUsageSql = @"
            SELECT COUNT(*) 
            FROM segments 
            WHERE coating_code = @Code";

        var usageCount = await ExecuteScalarAsync<int>(checkUsageSql, new { Code = code }, cancellationToken);
        
        if (usageCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete coating '{code}' as it is used in {usageCount} segments");
        }

        const string sql = "DELETE FROM catalogs.coatings WHERE code = @Code";
        
        var affectedRows = await ExecuteAsync(sql, new { Code = code }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Coating with code '{code}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Coating>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        const string sql = @"
            SELECT c.code, c.name, c.type, c.manufacturer, c.specification, c.created_at as CreatedAt
            FROM catalogs.coatings c 
            WHERE LOWER(c.name) LIKE LOWER(@SearchTerm) OR LOWER(c.code) LIKE LOWER(@SearchTerm)
            ORDER BY c.name";

        return await QueryAsync<Coating>(sql, new { SearchTerm = $"%{searchTerm}%" }, cancellationToken);
    }

    public async Task<IEnumerable<Coating>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        const string sql = @"
            SELECT c.code, c.name, c.type, c.manufacturer, c.specification, c.created_at as CreatedAt
            FROM catalogs.coatings c 
            WHERE c.type = @Type
            ORDER BY c.name";

        return await QueryAsync<Coating>(sql, new { Type = type }, cancellationToken);
    }

    public async Task<IEnumerable<Coating>> GetByManufacturerAsync(string manufacturer, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manufacturer);

        const string sql = @"
            SELECT c.code, c.name, c.type, c.manufacturer, c.specification, c.created_at as CreatedAt
            FROM catalogs.coatings c 
            WHERE c.manufacturer = @Manufacturer
            ORDER BY c.name";

        return await QueryAsync<Coating>(sql, new { Manufacturer = manufacturer }, cancellationToken);
    }

    public async Task<IEnumerable<Coating>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        return await SearchByNameAsync(searchTerm, cancellationToken);
    }
}

/// <summary>
/// Repository implementation for Fluid catalog entities
/// </summary>
public class FluidRepository : BaseRepository<Fluid, string>, IFluidRepository
{
    public FluidRepository(IDbConnectionFactory connectionFactory) 
        : base(connectionFactory)
    {
    }

    public override async Task<Fluid?> GetByIdAsync(string code, CancellationToken cancellationToken = default)
    {
        return await GetByCodeAsync(code, cancellationToken);
    }

    public async Task<Fluid?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        const string sql = @"
            SELECT f.code, f.name, f.corrosivity, f.density, f.viscosity, 
                   f.pressure_rating as PressureRating, f.created_at as CreatedAt
            FROM catalogs.fluids f 
            WHERE f.code = @Code";

        return await QuerySingleOrDefaultAsync<Fluid>(sql, new { Code = code }, cancellationToken);
    }

    public override async Task<IEnumerable<Fluid>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT f.code, f.name, f.corrosivity, f.density, f.viscosity, 
                   f.pressure_rating as PressureRating, f.created_at as CreatedAt
            FROM catalogs.fluids f 
            ORDER BY f.name";

        return await QueryAsync<Fluid>(sql, cancellationToken: cancellationToken);
    }

    public override async Task<string> CreateAsync(Fluid fluid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        // Check if fluid code already exists
        const string checkSql = @"
            SELECT COUNT(*) 
            FROM catalogs.fluids 
            WHERE code = @Code";

        var codeExists = await ExecuteScalarAsync<int>(checkSql, new { fluid.Code }, cancellationToken) > 0;
        
        if (codeExists)
        {
            throw new InvalidOperationException($"Fluid with code '{fluid.Code}' already exists");
        }

        const string sql = @"
            INSERT INTO catalogs.fluids (code, name, corrosivity, density, viscosity, pressure_rating, created_at)
            VALUES (@Code, @Name, @Corrosivity, @Density, @Viscosity, @PressureRating, @CreatedAt)
            RETURNING code";

        fluid.CreatedAt = DateTime.UtcNow;

        var parameters = new
        {
            fluid.Code,
            fluid.Name,
            fluid.Corrosivity,
            fluid.Density,
            fluid.Viscosity,
            fluid.PressureRating,
            fluid.CreatedAt
        };

        var result = await ExecuteScalarAsync<string>(sql, parameters, cancellationToken);
        return result ?? fluid.Code;
    }

    public override async Task UpdateAsync(Fluid fluid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        const string sql = @"
            UPDATE catalogs.fluids 
            SET name = @Name,
                corrosivity = @Corrosivity,
                density = @Density,
                viscosity = @Viscosity,
                pressure_rating = @PressureRating
            WHERE code = @Code";

        var parameters = new
        {
            fluid.Code,
            fluid.Name,
            fluid.Corrosivity,
            fluid.Density,
            fluid.Viscosity,
            fluid.PressureRating
        };

        var affectedRows = await ExecuteAsync(sql, parameters, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Fluid with code '{fluid.Code}' not found for update");
        }
    }

    public override async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        const string sql = "DELETE FROM catalogs.fluids WHERE code = @Code";
        
        var affectedRows = await ExecuteAsync(sql, new { Code = code }, cancellationToken);
        
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Fluid with code '{code}' not found for deletion");
        }
    }

    public async Task<IEnumerable<Fluid>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        const string sql = @"
            SELECT f.code, f.name, f.corrosivity, f.density, f.viscosity, 
                   f.pressure_rating as PressureRating, f.created_at as CreatedAt
            FROM catalogs.fluids f 
            WHERE LOWER(f.name) LIKE LOWER(@SearchTerm) OR LOWER(f.code) LIKE LOWER(@SearchTerm)
            ORDER BY f.name";

        return await QueryAsync<Fluid>(sql, new { SearchTerm = $"%{searchTerm}%" }, cancellationToken);
    }

    public async Task<IEnumerable<Fluid>> GetByCorrosivityAsync(string corrosivity, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corrosivity);

        const string sql = @"
            SELECT f.code, f.name, f.corrosivity, f.density, f.viscosity, 
                   f.pressure_rating as PressureRating, f.created_at as CreatedAt
            FROM catalogs.fluids f 
            WHERE f.corrosivity = @Corrosivity
            ORDER BY f.name";

        return await QueryAsync<Fluid>(sql, new { Corrosivity = corrosivity }, cancellationToken);
    }

    public async Task<IEnumerable<Fluid>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        return await SearchByNameAsync(searchTerm, cancellationToken);
    }

    public async Task<IEnumerable<Fluid>> GetCorrosiveFluidsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT f.code, f.name, f.corrosivity, f.density, f.viscosity, 
                   f.pressure_rating as PressureRating, f.created_at as CreatedAt
            FROM catalogs.fluids f 
            WHERE f.corrosivity IS NOT NULL 
            AND LOWER(f.corrosivity) NOT IN ('none', 'low', 'minimal')
            ORDER BY f.corrosivity DESC, f.name";

        return await QueryAsync<Fluid>(sql, cancellationToken: cancellationToken);
    }
}