using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Persistence.Seed;

/// <summary>Reference data inserted by the migrations (fixed ids so migrations stay deterministic).</summary>
public static class SeedData
{
    public static readonly Customer[] Customers =
    [
        new() { Id = 1, FirstName = "Aung", LastName = "Min", Email = "aung.min@example.com" },
        new() { Id = 2, FirstName = "Mei Ling", LastName = "Tan", Email = "meiling.tan@example.com" },
        new() { Id = 3, FirstName = "Rajesh", LastName = "Kumar", Email = "rajesh.kumar@example.com" },
    ];

    public static readonly Product[] Products =
    [
        new() { Id = 1, Name = "Day Pass", Description = "Single-day gym access.", Price = 25.00m, Currency = "SGD", PaymentType = PaymentType.OneTime },
        new() { Id = 2, Name = "Class Drop-in", Description = "One group fitness class of your choice.", Price = 18.50m, Currency = "SGD", PaymentType = PaymentType.OneTime },
        new() { Id = 3, Name = "Monthly Membership", Description = "Unlimited access, billed monthly to a saved card.", Price = 99.00m, Currency = "SGD", PaymentType = PaymentType.Recurring },
        new() { Id = 4, Name = "Annual Membership", Description = "Unlimited access for 12 months, renewed with a saved card.", Price = 899.00m, Currency = "SGD", PaymentType = PaymentType.Recurring },
    ];

    public static ModelBuilder ApplySeed(this ModelBuilder modelBuilder)
    {
        // Seed customers as anonymous scalar objects so the Cards navigation never reaches HasData.
        modelBuilder.Entity<Customer>().HasData(Customers.Select(c => new
        {
            c.Id,
            c.FirstName,
            c.LastName,
            c.Email,
        }));
        modelBuilder.Entity<Product>().HasData(Products);
        return modelBuilder;
    }
}
