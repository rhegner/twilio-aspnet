using Microsoft.AspNetCore.Mvc;
using Twilio.AspNet.Core;
using Twilio.Clients;
using Twilio.TwiML;

namespace dotnet6.Controllers;

[ApiController]
[Route("[controller]")]
public class SmsController : TwilioController
{
    [ValidateRequest(UrlOverride = "https://??????.ngrok.io/sms")]
    [HttpGet, HttpPost]
    public TwiMLResult Index()
    {
        var messagingResponse = new MessagingResponse();
        messagingResponse.Message("The Robots are coming! Head for the hills!!");

        return TwiML(messagingResponse);
    }
}
