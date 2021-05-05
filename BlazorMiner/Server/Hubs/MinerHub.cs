using BlazorMiner.Server.Data;
using BlazorMiner.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorMiner.Server.Hubs
{
    [Authorize]
    public class MinerHub : Hub<IMinerClient>, IMinerHub
    {
        public MinerHub(ApplicationDbContext applicationDbContext, RoomPinValidator roomPinValidator)
        {
            ApplicationDbContext = applicationDbContext;
            RoomPinValidator = roomPinValidator;
        }

        public ApplicationDbContext ApplicationDbContext { get; }
        public RoomPinValidator RoomPinValidator { get; }

        private async Task<string> GetUserNameAsync(string userId = null)
        {
            userId ??= Context.UserIdentifier;
            var email = (await ApplicationDbContext.Users.FindAsync(userId)).Email;
            return $"{email[0..email.IndexOf('@')]}-{userId[0..3]}";
        }

        public async Task SendMessageToLobbyAsync(string message) =>
            await Clients.All.RecieveLobbyMessageAsync(new Message
            {
                Date = DateTime.Now,
                Text = message?.Length > 100 ? message[0..100] : message,
                User = await GetUserNameAsync()
            });

        private static ConcurrentDictionary<Guid, (string Pin, Room Room)> Rooms { get; } = new ConcurrentDictionary<Guid, (string, Room)>();

        public Task<string> GetUserIdAsync() => Task.FromResult(Context.UserIdentifier);

        public async Task<List<Room>> UpdateRoomsAsync()
        {
            foreach (var (id, _) in Rooms.Where(r => r.Value.Room.Date < DateTime.Today.AddHours(-1)).ToList())
                Rooms.Remove(id, out _);
            var rooms = Rooms.Select(r => r.Value.Room).OrderBy(r => r.Date).ToList();
            await Clients.All.UpdateRoomsListAsync(rooms);
            return rooms;
        }

        private async Task<User> GetCurrentUserAsync() =>
            new User
            {
                Id = Context.UserIdentifier,
                Name = await GetUserNameAsync()
            };

        public async Task<Room> CreateRoomAsync(string pin, string name)
        {
            if (!RoomPinValidator.IsValidPin(pin))
                throw new HubException("Invalid PIN.");

            var user = await GetCurrentUserAsync();
            var room = new Room
            {
                Date = DateTime.Now,
                Id = Guid.NewGuid(),
                Name = name,
                Started = false,
                Host = user,
                Users = new List<User> { user }
            };
            Rooms.TryAdd(room.Id, (pin, room));
            await UpdateRoomsAsync();
            return room;
        }

        public async Task DeleteRoomAsync(Guid roomId)
        {
            if (Rooms.TryGetValue(roomId, out var item) && item.Room.Host?.Id == Context.UserIdentifier)
                Rooms.Remove(roomId, out _);
            await UpdateRoomsAsync();
        }

        public async Task<EnterRoomResult> EnterRoomAsync(Guid roomId, string pin)
        {
            var check = (RoomExists: Rooms.TryGetValue(roomId, out var item),
                         PinIsCorrect: item.Pin == pin,
                         UserIsInRoom: item.Room?.Users.Any(u => u.Id == Context.UserIdentifier),
                         GameInProgress: item.Room?.Started == true);
            var result = check switch
            {
                (true, true, false, false) => EnterRoomResult.Ok,
                (true, true, false, true) => EnterRoomResult.GameAlreadyInProgress,
                (true, true, true, _) => EnterRoomResult.UserAlreadyInRoom,
                (true, false, _, _) => EnterRoomResult.InvalidPin,
                (false, _, _, _) => EnterRoomResult.InvalidRoomId,
            };
            if (result == EnterRoomResult.Ok)
            {
                item.Room.Users.Add(await GetCurrentUserAsync());
                await UpdateRoomsAsync();
            }
            return result;
        }

        private static ConcurrentDictionary<Guid, (GameState state, Dictionary<(int x, int y), int> map)> Games { get; } = new ConcurrentDictionary<Guid, (GameState game, Dictionary<(int x, int y), int> map)>();

        public async Task UpdateGameStateAsync(Guid roomId)
        {
            if (Rooms.TryGetValue(roomId, out var item) && Games.TryGetValue(roomId, out var game))
                await Clients.Users(item.Room.Users.Select(u => u.Id).ToList()).UpdateGameStateAsync(game.state, game.state.TurnEnds.HasValue ? (game.state.TurnEnds.Value - DateTime.Now).TotalMilliseconds : (double?)null);
        }

        private static IEnumerable<(int x, int y)> GetNeighbors(Dictionary<(int x, int y), int> map, int x, int y) =>
            new[] {
                (x - 1, y - 1), (x, y - 1), (x + 1, y - 1),
                (x - 1, y), (x + 1, y),
                (x - 1, y + 1), (x, y + 1), (x + 1, y + 1)
            }.Where(map.ContainsKey);

        private static void NextPlayer(GameState state)
        {
            state.CurrentUserId = state.Users[(state.Users.FindIndex(u => u.Id == state.CurrentUserId) + 1) % state.Users.Count].Id;
            state.TurnEnds = DateTime.Now.AddSeconds(5);
        }

        public async Task StartGameAsync(Guid roomId)
        {
            User user = null;
            if (Rooms.TryGetValue(roomId, out var item) && !item.Room.Started && (user = item.Room.Users.FirstOrDefault(u => u.Id == Context.UserIdentifier)) != null)
            {
                item.Room.Started = true;
                var random = new Random();
                var state = new GameState
                {
                    GameId = roomId,
                    TurnEnds = DateTime.Now.AddSeconds(30),
                    CurrentUserId = item.Room.Users.OrderBy(_ => random.Next()).First().Id,
                    Size = Math.Min(Math.Max(7, item.Room.Users.Count), 12),
                    Users = item.Room.Users.Select(u => new User { Id = u.Id, Name = u.Name, Points = 0 }).ToList()
                };
                user = state.Users.First(u => u.Id == Context.UserIdentifier);
                var map = Enumerable.Range(1, state.Size).SelectMany(x => Enumerable.Range(1, state.Size).Select(y => (x, y))).ToDictionary(c => c, _ => 0);
                state.Map = new int?[state.Size * state.Size];
                foreach (var ((x, y), v) in map.OrderBy(_ => random.Next()).Take((int)(0.15 * state.Size * state.Size)).ToList())
                {
                    map[(x, y)] = -1;
                    foreach (var neighbor in GetNeighbors(map, x, y))
                    {
                        if (map[neighbor] != -1)
                            map[neighbor] += 1;
                    }
                }

                Games.TryAdd(roomId, (state, map));
                _ = Task.Run(async () =>
                {
                    while (state.TurnEnds != null)
                    {
                        await Task.Delay(500);
                        if (state.TurnEnds < DateTime.Now)
                        {
                            user.Points -= 10;
                            NextPlayer(state);
                        }
                        await UpdateGameStateAsync(roomId);
                    }
                });
            }
            await UpdateGameStateAsync(roomId);
        }

        private static int GetCoordinate(int x, int y, int size) => size * (y - 1) + (x - 1);

        public async Task MakeMoveAsync(Guid roomId, int x, int y)
        {
            User user = null;
            int coordinate;
            if (Games.TryGetValue(roomId, out var game) &&
                x > 0 && y > 0 && (coordinate = GetCoordinate(x, y, game.state.Size)) < game.state.Map.Length &&
                game.state.Map[coordinate] == null &&
                (user = game.state.Users.FirstOrDefault(u => u.Id == Context.UserIdentifier)) != null &&
                game.map.TryGetValue((x, y), out var value) &&
                game.state.CurrentUserId == Context.UserIdentifier)
            {
                game.state.Map[coordinate] = value;
                switch (value)
                {
                    case -1:
                        user.Points += 10;
                        break;
                    case 0:
                        RevealNeighbors(x, y);
                        void RevealNeighbors(int x, int y)
                        {
                            foreach (var neighbor in GetNeighbors(game.map, x, y).Where(n => game.state.Map[GetCoordinate(n.x, n.y, game.state.Size)] == null))
                            {
                                if ((game.state.Map[GetCoordinate(neighbor.x, neighbor.y, game.state.Size)] = game.map[neighbor]) == 0)
                                    RevealNeighbors(neighbor.x, neighbor.y);
                            }
                        }
                        break;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                        user.Points += value;
                        break;
                }

                NextPlayer(game.state);
                if (game.state.Map.Count(m => m == -1) == game.map.Count(m => m.Value == -1))
                {
                    game.state.Map = game.map.OrderBy(kv => kv.Key.y).ThenBy(kv => kv.Key.x).Select(kv => (int?)kv.Value).ToArray();
                    game.state.TurnEnds = null;
                    await UpdateGameStateAsync(roomId);
                    Games.Remove(roomId, out _);
                    Rooms.Remove(roomId, out _);
                    await UpdateRoomsAsync();
                }
                else
                {
                    game.state.TurnEnds = DateTime.Now.AddSeconds(5);
                    await UpdateGameStateAsync(roomId);
                }
            }
        }
    }
}