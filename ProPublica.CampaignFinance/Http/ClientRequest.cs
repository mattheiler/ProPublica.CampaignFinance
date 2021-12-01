namespace ProPublica.CampaignFinance.Http
{
    public class ClientRequest
    {
        public ClientRequest(string path, ClientRequestQuery query)
        {
            Path = path;
            Query = query;
        }

        public string Path { get; }

        public ClientRequestQuery Query { get; }
    }
}