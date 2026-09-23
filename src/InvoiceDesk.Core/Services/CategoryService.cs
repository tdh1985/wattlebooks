// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class CategoryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<Category>> ListAsync(Direction? direction = null, bool includeArchived = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Category> query = db.Categories.AsNoTracking();
        if (direction is { } d) query = query.Where(c => c.Direction == d);
        if (!includeArchived) query = query.Where(c => !c.IsArchived);
        return await query.OrderBy(c => c.Direction).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
    }

    public async Task<Category> SaveAsync(Category category)
    {
        var name = Text.Clean(category.Name);
        if (name.Length == 0) throw new ValidationException("Category name is required.");

        await using var db = await factory.CreateDbContextAsync();
        Category entity;
        if (category.Id == 0)
        {
            var last = await db.Categories.Where(c => c.Direction == category.Direction).MaxAsync(c => (int?)c.SortOrder) ?? -1;
            entity = new Category { Direction = category.Direction, SortOrder = last + 1 };
            db.Categories.Add(entity);
        }
        else
        {
            entity = await db.Categories.FindAsync(category.Id) ?? throw new ValidationException("That category no longer exists.");
        }

        entity.Name = name;
        entity.IsArchived = category.IsArchived;
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task SetArchivedAsync(int id, bool archived)
    {
        await using var db = await factory.CreateDbContextAsync();
        var category = await db.Categories.FindAsync(id);
        if (category is null) return;
        category.IsArchived = archived;
        await db.SaveChangesAsync();
    }

    // payments need somewhere to land even if sales was renamed or archived
    public async Task<Category> GetSalesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var income = db.Categories.AsNoTracking().Where(c => c.Direction == Direction.In);
        var sales = await income.OrderBy(c => c.IsArchived).FirstOrDefaultAsync(c => c.Name == "Sales")
                    ?? await income.Where(c => !c.IsArchived).OrderBy(c => c.SortOrder).FirstOrDefaultAsync();
        if (sales is not null) return sales;

        sales = new Category { Name = "Sales", Direction = Direction.In };
        db.Categories.Add(sales);
        await db.SaveChangesAsync();
        return sales;
    }

    // a written-off shortfall needs a home even if the seeded one was removed
    public async Task<Category> GetBankFeesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var fees = await db.Categories.AsNoTracking()
            .Where(c => c.Direction == Direction.Out && c.Name == "Bank fees")
            .OrderBy(c => c.IsArchived)
            .FirstOrDefaultAsync();
        if (fees is not null) return fees;

        fees = new Category { Name = "Bank fees", Direction = Direction.Out };
        db.Categories.Add(fees);
        await db.SaveChangesAsync();
        return fees;
    }
}
