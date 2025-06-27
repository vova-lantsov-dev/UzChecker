using System.ComponentModel.DataAnnotations;

namespace UzChecker.AppHost.Options;

public class UzDate
{
    [Required]
    public string Date { get; set; }
    [Required, MinLength(1)]
    public string[] Trains { get; set; }
}