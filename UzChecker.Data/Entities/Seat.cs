using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UzChecker.Data.Entities;

public class Seat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [ForeignKey(nameof(Wagon))]
    public string WagonId { get; set; }
    public Wagon Wagon { get; set; }

    [ForeignKey(nameof(Trip))]
    public int TripId { get; set; }
    public Trip Trip { get; set; }

    [Required]
    public int SeatNumber { get; set; }
    [Required]
    public string WagonNumber { get; set; }
}