using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetellIntegrationApi.Models;

/// <summary>
/// Model representing a request to calculate a quotation estimate.
/// </summary>
public class QuoteRequest
{
    private string _customerName = string.Empty;
    
    public string CustomerName 
    { 
        get => _customerName; 
        set => _customerName = value; 
    }

    [JsonPropertyName("customer_name")]
    public string CustomerNameSnake 
    { 
        get => _customerName; 
        set => _customerName = value; 
    }

    public string Mobile { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    private string _eventType = string.Empty;
    
    public string EventType 
    { 
        get => _eventType; 
        set => _eventType = value; 
    }

    [JsonPropertyName("event_type")]
    public string EventTypeSnake 
    { 
        get => _eventType; 
        set => _eventType = value; 
    }

    private string _eventDate = string.Empty;
    
    public string EventDate 
    { 
        get => _eventDate; 
        set => _eventDate = value; 
    }

    [JsonPropertyName("event_date")]
    public string EventDateSnake 
    { 
        get => _eventDate; 
        set => _eventDate = value; 
    }

    public string Location { get; set; } = string.Empty;

    private int _guestCount;
    
    public int GuestCount 
    { 
        get => _guestCount; 
        set => _guestCount = value; 
    }

    [JsonPropertyName("guest_count")]
    public int GuestCountSnake 
    { 
        get => _guestCount; 
        set => _guestCount = value; 
    }

    public List<string> Requirements { get; set; } = new();
}
