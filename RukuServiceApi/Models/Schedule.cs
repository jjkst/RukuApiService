using System;
using System.ComponentModel.DataAnnotations;

namespace RukuServiceApi.Models;

public class Schedule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string ContactName { get; set; } = string.Empty;

    [Required]
    public DateTime SelectedDate { get; set; }

    [Required]
    public List<string> Services { get; set; } = new();

    [Required]
    public List<string> Timeslots { get; set; } = new();

    public string? Note { get; set; }

    public string? Uid { get; set; }
}
