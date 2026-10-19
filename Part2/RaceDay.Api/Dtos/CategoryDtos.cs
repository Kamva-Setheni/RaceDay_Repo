using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class CategoryRequest
{
    /// <summary>For example "Under 20", "Senior", "10km" or "21km".</summary>
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999.99")]
    public decimal Distance { get; set; }

    [Range(typeof(decimal), "0", "99999.99")]
    public decimal EntryFee { get; set; }
}

public record CategoryResponse(int CategoryId, int EventId, string Name, decimal Distance, decimal EntryFee);
