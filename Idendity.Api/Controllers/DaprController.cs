using Dapr;
using Microsoft.AspNetCore.Mvc;

namespace Idendity.Api.Controllers;

/// <summary>
/// Controller for Dapr-specific endpoints (pub/sub, bindings)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DaprController : ControllerBase
{
    private readonly ILogger<DaprController> _logger;

    public DaprController(ILogger<DaprController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Dapr pub/sub subscription handler for user events
    /// Example: Other services can subscribe to user events
    /// </summary>
    [Topic("pubsub", "user-events")]
    [HttpPost("user-events")]
    public IActionResult HandleUserEvent([FromBody] UserEventMessage message)
    {
        _logger.LogInformation("Received user event: {EventType} for user {UserId}", 
            message.EventType, message.UserId);

        // Process the event based on type
        switch (message.EventType)
        {
            case "UserRegistered":
                // Handle user registered event
                _logger.LogInformation("Processing UserRegistered event for {UserId}", message.UserId);
                break;
            
            case "UserDeactivated":
                // Handle user deactivated event
                _logger.LogInformation("Processing UserDeactivated event for {UserId}", message.UserId);
                break;
            
            default:
                _logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                break;
        }

        return Ok();
    }
}

public record UserEventMessage(
    string EventType,
    Guid UserId,
    DateTime Timestamp,
    Dictionary<string, object>? Metadata = null
);


