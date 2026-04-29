namespace LegendaryLibraryNS.Models
{
    public class ErrorResponse
    {
        public string ErrorCode { get; set;} = "";
        public string ErrorMessage { get; set;} = "";
        public int NumericErrorCode;
        public string OriginatingService { get; set;} = "";
        public string Intent { get; set;} = "";
    }
}