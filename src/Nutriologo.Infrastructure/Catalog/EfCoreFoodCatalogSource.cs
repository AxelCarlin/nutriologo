namespace Nutriologo.Infrastructure.Catalog;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Catalog;
using Nutriologo.Infrastructure.Persistence;

public class EfCoreFoodCatalogSource : IFoodCatalogSource
{
    private readonly AppDbContext _db;
    public EfCoreFoodCatalogSource(AppDbContext db) => _db = db;

    public IReadOnlyList<FoodCatalogItem> Todos() =>
        _db.Alimentos.AsNoTracking()
            .Select(a => new FoodCatalogItem(a.Id, a.Nombre, null, null, null))
            .ToList();
}
