using System;
using System.Collections.Generic;

namespace BlazorMiner.Shared
{
public class Room
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public User Host { get; set; }
    public string Name { get; set; }
    public bool Started { get; set; }
    public List<User> Users { get; set; }
}
}
