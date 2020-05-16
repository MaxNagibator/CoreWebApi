namespace WebApi
{
    public interface ILog
    {
        public string LogType { get; }
    }

    public class QueryRequestData : ILog
    {
        public string LogType => "QueryRequestData";

        public string Scheme { get; set; }
        public string Host { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public string BodyAsText { get; set; }
    }

    public class QueryResponseData : ILog
    {
        public string LogType => "QueryResponseData";

        public string StatusCode { get; set; }
        public string BodyAsText { get; set; }
    }
}
