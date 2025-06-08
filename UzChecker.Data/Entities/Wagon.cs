using System.ComponentModel.DataAnnotations;

namespace UzChecker.Data.Entities;

public class Wagon
{
    [Key]
    public string Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; }
}