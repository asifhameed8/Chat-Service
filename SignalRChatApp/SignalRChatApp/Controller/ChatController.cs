using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using SignalRChatApp.Hubs;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.RegularExpressions;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SignalRChatApp.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly IDistributedCache _cache;

        public ChatController(IHubContext<ChatHub> chatHubContext, IConnectionMultiplexer redisConnection, IDistributedCache cache)
        {
            _chatHubContext = chatHubContext;
            _redisConnection = redisConnection;
            _cache = cache;
        }
        // GET: api/<ChatController>
        [HttpGet]
        [Route("GetLast5Messages")]
        public async Task<IActionResult> GetLastMessages(string room)
        {
            try
            {
                // Connect to Redis and retrieve the last 'count' messages for the room
                var db = _redisConnection.GetDatabase();
                var messages = await db.ListRangeAsync($"chatroom:{room}", -5, -1); // Get the last 'count' messages

                // Convert the message objects from JSON to a list of chat messages
                var chatMessages = messages.Select(msg => JsonSerializer.Deserialize<ChatMessage>(msg)).ToList();
                return Ok(chatMessages);
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }
        [HttpGet]
        [Route("GetChatHistory")]
        public IActionResult GetChatHistory(string room)
        {
            try
            {
                // Connect to Redis and retrieve all messages for the specified room
                var db = _redisConnection.GetDatabase();
                var messages = db.ListRange($"chatroom:{room}");
                // Convert the message objects from JSON to a list of chat messages
                var chatMessages = messages.Select(msg => JsonSerializer.Deserialize<ChatMessage>(msg)).ToList();

                return Ok(chatMessages);
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("sendmessage")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                // Create a message object
                var chatMessage = new ChatMessage
                {
                    Username = request.Username,
                    Message = request.Message,
                    Room = request.Room,
                    Timestamp = DateTime.UtcNow
                };

                // Serialize the message object to JSON
                var messageJson = JsonSerializer.Serialize(chatMessage);
                // Store the message in Redis
                var db = _redisConnection.GetDatabase();
                await db.ListRightPushAsync($"chatroom:{request.Room}", messageJson);
                // Broadcast the message to all clients in the room
                await _chatHubContext.Clients.Group(request.Room).SendAsync("ReceiveMessage", request.Username, request.Message);
                return Ok(chatMessage);
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("joinroom")]
        public async Task<IActionResult> JoinRoom([FromBody] LeaveRoomRequest request)
        {
            try
            {
                await _chatHubContext.Groups.AddToGroupAsync(request.UserName, request.Room);
                return Ok();
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("leaveroom")]
        public async Task<IActionResult> LeaveRoom([FromBody] LeaveRoomRequest request)
        {
            try
            {
                await _chatHubContext.Groups.RemoveFromGroupAsync(request.UserName, request.Room);
                return Ok();
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("changeroom")]
        public async Task<IActionResult> ChangeRoom([FromBody] ChangeRoomRequest request)
        {
            try
            {
                await _chatHubContext.Groups.RemoveFromGroupAsync(request.UserName, request.Room);
                await _chatHubContext.Groups.AddToGroupAsync(request.UserName, request.Room);
                var connectionId = HttpContext.Connection.Id; // Get the caller's connection ID
                await _chatHubContext.Clients.Client(connectionId).SendAsync("RoomChanged", request.newRoom);
                return Ok();
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpGet]
        [Route("TotalLoggedInUser")]
        public async Task<IActionResult> TotalLoggedInUser()
        {
            try
            {
                var counterKey = "login_counter";

                var currentCount = await _cache.GetStringAsync(counterKey);
                return Ok(string.IsNullOrEmpty(currentCount) ? 0 : int.Parse(currentCount));
            }
            catch (Exception ex)
            {
                return Conflict(ex.Message);
            }
        }
    }
}
