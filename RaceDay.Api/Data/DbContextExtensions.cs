using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api.Data;

public static class DbContextExtensions
{
    // The foreign keys use NO ACTION (as in the Part 1 SQL script), so child rows are removed
    // explicitly here. Removes the results, payments and enrolments of the given categories.
    public static async Task RemoveEnrolmentsForCategoriesAsync(this RaceDayDbContext db, List<int> categoryIds)
    {
        if (categoryIds.Count == 0) return;

        var enrolments = await db.Enrolments
            .Where(e => categoryIds.Contains(e.CategoryId))
            .ToListAsync();
        var enrolmentIds = enrolments.Select(e => e.EnrolmentId).ToList();

        var results = await db.Results.Where(r => enrolmentIds.Contains(r.EnrolmentId)).ToListAsync();
        var payments = await db.Payments.Where(p => enrolmentIds.Contains(p.EnrolmentId)).ToListAsync();

        db.Results.RemoveRange(results);
        db.Payments.RemoveRange(payments);
        db.Enrolments.RemoveRange(enrolments);
    }
}
