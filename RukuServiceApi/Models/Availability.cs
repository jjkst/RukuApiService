using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RukuServiceApi.Models;

public class Availability
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public List<string> Services { get; set; } = new();

    [Required]
    public List<string> Timeslots { get; set; } = new();
}
