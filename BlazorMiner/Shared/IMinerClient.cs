using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorMiner.Shared
{
    public interface IMinerClient
    {
        Task RecieveLobbyMessageAsync(Message message);
        Task UpdateRoomsListAsync(List<Room> rooms);
        Task UpdateGameStateAsync(GameState gameState, double? turnEndsInMilliseconds);
    }
}
