using System.ComponentModel.DataAnnotations;

namespace UzChecker.AppHost.Options;

public class UzSubscription
{
    [Required]
    public string StationFrom { get; set; }
    [Required]
    public string StationTo { get; set; }
    [Required, MinLength(1)]
    public string[] TrainTypes { get; set; }
    [Required, MinLength(1)]
    public UzDate[] Dates { get; set; }
}