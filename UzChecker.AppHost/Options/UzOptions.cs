using System.ComponentModel.DataAnnotations;

namespace UzChecker.AppHost.Options;

public class UzOptions
{
    [Required]
    public string StationFrom { get; set; }
    [Required]
    public string StationTo { get; set; }
    [Required]
    public string Date { get; set; }
    [Required, MinLength(1)]
    public string[] Types { get; set; }
    [Required, MinLength(1)]
    public string[] Trains { get; set; }
    [Required, Range(1, int.MaxValue)]
    public int SeatsCount { get; set; }
}