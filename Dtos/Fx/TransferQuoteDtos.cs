using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Fx;

public class CreateTransferQuoteRequestDto
{
    [Required]
    [MaxLength(10)]
    public string SourceCountryCode { get; set; } = "";

    [Required]
    [MaxLength(10)]
    public string DestinationCountryCode { get; set; } = "";

    [Required]
    [MaxLength(10)]
    public string SourceCurrencyCode { get; set; } = "";

    [Required]
    [MaxLength(10)]
    public string DestinationCurrencyCode { get; set; } = "";

    [Required]
    public TransferType TransferType { get; set; }

    [Range(1, double.MaxValue)]
    public decimal SourceAmount { get; set; }
}