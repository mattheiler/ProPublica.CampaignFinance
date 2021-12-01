namespace ProPublica.CampaignFinance.Http
{
    public class ClientRequestBuilder
    {
        public ClientRequestBuilder(string path)
        {
            Path = path;
        }

        public string Path { get; set; }

        public ClientRequestQuery Query { get; } = new ClientRequestQuery();

        public ClientRequestBuilder Page(int index)
        {
            Query["offset"] = (index * /* size */ 20).ToString();
            return this;
        }

        public ClientRequest GetRequest()
        {
            return new ClientRequest(Path.TrimStart('/'), new ClientRequestQuery(Query));
        }
    }
}