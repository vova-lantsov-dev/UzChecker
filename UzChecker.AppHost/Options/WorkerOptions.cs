using System.ComponentModel.DataAnnotations;

namespace UzChecker.AppHost.Options;

public class WorkerOptions
{
    [Range(1, int.MaxValue)]
    public int IntervalInSeconds { get; set; }
}