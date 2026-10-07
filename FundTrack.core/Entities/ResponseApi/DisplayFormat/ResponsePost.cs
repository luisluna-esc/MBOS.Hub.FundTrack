using System.Net;
using System.Text.Json.Serialization;
using FundTrack.core.Entities.ResponseApi.Details;

namespace FundTrack.core.Entities.ResponseApi.DisplayFormat;

public class ResponsePost
{
    public Message[] Messages { get; set; } = [];
    public long Id { get; set; }
    [JsonIgnore]
    public HttpStatusCode StatusCode { get; set; }
}