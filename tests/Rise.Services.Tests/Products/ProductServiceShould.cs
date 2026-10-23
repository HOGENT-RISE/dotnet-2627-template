using Microsoft.EntityFrameworkCore;
using Rise.Domain.Products;
using Rise.Persistence;
using Rise.Services.Products;
using Rise.Shared.Common;
using Rise.Shared.Products;

namespace Rise.Services.Tests.Products;

public class ProductServiceShould(
    DatabaseFixture database,
    ITestOutputHelper output) : DatabaseTest(database, output)
{
    [Fact]
    public async Task CreateProduct()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = new ProductService(dbContext);

        var request = new ProductRequest.Create
        {
            Name = "Product A",
            Description = "Description A"
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(ResultStatus.Created);
        result.Value.ProductId.ShouldBeGreaterThan(0);

        await using var verificationContext = CreateDbContext();

        var product = await verificationContext.Products
            .SingleAsync(p => p.Id == result.Value.ProductId);

        product.Name.ShouldBe("Product A");
        product.Description.ShouldBe("Description A");
    }

    [Fact]
    public async Task NotCreateProductWhenNameAlreadyExists()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        dbContext.Products.Add(new Product
        {
            Name = "Product A",
            Description = "Original Description"
        });

        await dbContext.SaveChangesAsync();

        var service = new ProductService(dbContext);

        var request = new ProductRequest.Create
        {
            Name = "Product A",
            Description = "New Description"
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Conflict);

        await using var verificationContext = CreateDbContext();

        var products = await verificationContext.Products.ToListAsync();

        products.Count.ShouldBe(1);
        products.Single().Description.ShouldBe("Original Description");
    }

    [Fact]
    public async Task AllowProductsWithDifferentNames()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = new ProductService(dbContext);

        var firstRequest = new ProductRequest.Create
        {
            Name = "Product A",
            Description = "Description A"
        };

        var secondRequest = new ProductRequest.Create
        {
            Name = "Product B",
            Description = "Description B"
        };

        // Act
        var firstResult = await service.CreateAsync(firstRequest);
        var secondResult = await service.CreateAsync(secondRequest);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        secondResult.IsSuccess.ShouldBeTrue();

        await using var verificationContext = CreateDbContext();

        var count = await verificationContext.Products.CountAsync();
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetProductsOrderedByNameByDefault()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Charlie", "Description C"),
            ("Alpha", "Description A"),
            ("Bravo", "Description B"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            Skip = 0,
            Take = 10
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(3);

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Alpha", "Bravo", "Charlie"]);
    }

    [Fact]
    public async Task SearchProductsByName()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Apple", "Fruit"),
            ("Banana", "Fruit"),
            ("Apple Juice", "Drink"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            SearchTerm = "Apple",
            Skip = 0,
            Take = 10
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Apple", "Apple Juice"]);
    }

    [Fact]
    public async Task SearchProductsByDescription()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Product A", "Electric vehicle"),
            ("Product B", "Diesel vehicle"),
            ("Product C", "Electric bicycle"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            SearchTerm = "Electric",
            Skip = 0,
            Take = 10
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Product A", "Product C"]);
    }

    [Fact]
    public async Task ReturnEmptyResultWhenNoProductsMatch()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Apple", "Fruit"),
            ("Banana", "Fruit"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            SearchTerm = "NonExisting",
            Skip = 0,
            Take = 10
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Products.ShouldBeEmpty();
    }

    [Fact]
    public async Task PaginateProducts()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Alpha", "Description A"),
            ("Bravo", "Description B"),
            ("Charlie", "Description C"),
            ("Delta", "Description D"),
            ("Echo", "Description E"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            Skip = 2,
            Take = 2
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(5);
        result.Value.Products.Count().ShouldBe(2);

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Charlie", "Delta"]);
    }

    [Fact]
    public async Task OrderProductsByNameDescending()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Alpha", "Description A"),
            ("Charlie", "Description C"),
            ("Bravo", "Description B"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            Skip = 0,
            Take = 10,
            OrderBy = nameof(Product.Name),
            OrderDescending = true
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Charlie", "Bravo", "Alpha"]);
    }

    [Fact]
    public async Task OrderProductsByNameAscending()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Charlie", "Description C"),
            ("Alpha", "Description A"),
            ("Bravo", "Description B"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            Skip = 0,
            Take = 10,
            OrderBy = nameof(Product.Name),
            OrderDescending = false
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        result.Value.Products
            .Select(p => p.Name)
            .ShouldBe(["Alpha", "Bravo", "Charlie"]);
    }

    [Fact]
    public async Task ReturnEmptyResultWhenNoProductsExist()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            Skip = 0,
            Take = 10
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Products.ShouldBeEmpty();
    }

    private static async Task SeedProductsAsync(ApplicationDbContext dbContext, params (string Name, string Description)[] products)
    {
        dbContext.Products.AddRange(products.Select(p => new Product
        {
            Name = p.Name,
            Description = p.Description
        }));

        await dbContext.SaveChangesAsync();
    }
    
    [Fact]
    public async Task ReturnFilteredTotalCountWhenSearchingWithPagination()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        await SeedProductsAsync(dbContext,
            ("Apple", "Fruit"),
            ("Apple Juice", "Drink"),
            ("Apple Pie", "Dessert"),
            ("Banana", "Fruit"),
            ("Orange", "Fruit"));

        var service = new ProductService(dbContext);

        var request = new QueryRequest.SkipTake
        {
            SearchTerm = "Apple",
            Skip = 1,
            Take = 1
        };

        // Act
        var result = await service.GetIndexAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // 5 products globally, 3 matching the search
        result.Value.TotalCount.ShouldBe(3);

        // Pagination returns only 1 product
        result.Value.Products.Count().ShouldBe(1);
        result.Value.Products.Single().Name.ShouldBe("Apple Juice");
    }
}