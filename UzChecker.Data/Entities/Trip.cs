﻿using System.ComponentModel.DataAnnotations;

namespace UzChecker.Data.Entities;

public class Trip
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string TrainNumber { get; set; }
}