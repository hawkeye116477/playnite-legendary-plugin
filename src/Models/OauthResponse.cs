using System;

namespace LegendaryLibraryNS.Models
{
    public class OauthResponse
    {
        public string Access_token { get; set; } = "";
        public long Expires_in;
        public DateTime Expires_at;
        public string Token_type { get; set; } = "";
        public string Refresh_token { get; set; } = "";
        public long Refresh_expires;
        public DateTime Refresh_expires_at;
        public string Account_id { get; set; } = "";
        public string Client_id { get; set; } = "";
        public bool Internal_client;
        public string Client_service { get; set; } = "";
        public string App { get; set; } = "";
        public string In_app_id { get; set; } = "";
        public string Device_id { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
