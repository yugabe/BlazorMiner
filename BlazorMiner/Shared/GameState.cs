using System;
using System.Collections.Generic;

namespace BlazorMiner.Shared
{
    public class GameState
    {
        public Guid GameId { get; set; }
        public string CurrentUserId { get; set; }
        public DateTime? TurnEnds { get; set; }
        public int Size { get; set; }
        public int?[] Map { get; set; }
        public List<User> Users { get; set; }
    }
}