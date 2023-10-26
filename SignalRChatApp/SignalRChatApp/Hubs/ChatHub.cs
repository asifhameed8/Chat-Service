using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace SignalRChatApp.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly IDistributedCache _cache;
        public ChatHub(IConnectionMultiplexer redisConnection, IDistributedCache cache)
        {
            _redisConnection = redisConnection;
            _cache = cache;
        }
        public async Task SendMessage(string username, string message, string room)
        {
            // Create a message object
            var chatMessage = new ChatMessage
            {
                Username = username,
                Message = message,
                Room = room,
                Timestamp = DateTime.UtcNow
            };

            // Serialize the message object to JSON
            var messageJson = JsonSerializer.Serialize(chatMessage);
            // Store the message in Redis
            var db = _redisConnection.GetDatabase();
            await db.ListRightPushAsync($"chatroom:{room}", messageJson);
            // Broadcast the message to all clients in the room
            //var UserLogin = await GetTotalUserLoginCount();
            await Clients.Group(room).SendAsync("ReceiveMessage", username, message);
        }

        public async Task JoinRoom(string room)
        {
            string connectionId = Context.ConnectionId;

            // Check if the user is already in the group using Redis
            var redis = _redisConnection.GetDatabase();
            var key = $"UserGroups:{room}";

            if (await redis.SetContainsAsync(key, connectionId))
            {
                // User is already in the group, do something
                // await IncrementUserLogin();
            }
            else
            {
                // User is not in the group, add them to the group
                await Groups.AddToGroupAsync(connectionId, room);

                // Update Redis to reflect the user's membership in the group
                await redis.SetAddAsync(key, connectionId);

                // You can perform additional actions here after the user has joined the group
                await IncrementLoginCounter();
            }
        }

        public async Task LeaveRoom(string room)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
            await DecrementLoginCounter();
        }
        public async Task ChangeRoom(string oldRoom, string newRoom)
        {
            await LeaveRoom(oldRoom);
            await JoinRoom(newRoom);
            await Clients.Caller.SendAsync("RoomChanged", newRoom);
        }
        private async Task IncrementLoginCounter()
        {
            var counterKey = "login_counter";
            // Retrieve the current login count from Redis
            var currentCount = await _cache.GetStringAsync(counterKey);
            int newCount = string.IsNullOrEmpty(currentCount) ? 1 : int.Parse(currentCount) + 1;

            // Save the updated count in Redis
            await _cache.SetStringAsync(counterKey, newCount.ToString());
        }

        private async Task IncrementUserLogin()
        {
            var counterKey = "login_User";
            // Retrieve the current login count from Redis
            var currentCount = await _cache.GetStringAsync(counterKey);
            int newCount = string.IsNullOrEmpty(currentCount) ? 1 : int.Parse(currentCount) + 1;

            // Save the updated count in Redis
            await _cache.SetStringAsync(counterKey, newCount.ToString());
        }
        private async Task DecrementLoginCounter()
        {
            var counterKey = "login_counter";

            // Retrieve the current login count from Redis
            var currentCount = await _cache.GetStringAsync(counterKey);
            int newCount = string.IsNullOrEmpty(currentCount) ? 0 : int.Parse(currentCount) - 1;

            if (newCount < 0)
            {
                newCount = 0; // Ensure the count doesn't go negative
            }

            // Save the updated count in Redis
            await _cache.SetStringAsync(counterKey, newCount.ToString());
        }

        public async Task<int> GetTotalLoginCount()
        {
            var counterKey = "login_counter";

            var currentCount = await _cache.GetStringAsync(counterKey);
            return string.IsNullOrEmpty(currentCount) ? 0 : int.Parse(currentCount);
        }
        public async Task<int> GetTotalUserLoginCount()
        {
            var counterKey = "login_User";

            var currentCount = await _cache.GetStringAsync(counterKey);
            return string.IsNullOrEmpty(currentCount) ? 0 : int.Parse(currentCount);
        }
    }
}
