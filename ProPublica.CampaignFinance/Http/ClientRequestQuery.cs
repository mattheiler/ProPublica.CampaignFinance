using System;
using System.Collections.Specialized;
using System.Linq;

namespace ProPublica.CampaignFinance.Http
{
    public sealed class ClientRequestQuery : NameValueCollection
    {
        internal ClientRequestQuery()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        internal ClientRequestQuery(ClientRequestQuery query)
            : base(query)
        {
        }

        public override string ToString()
        {
            return string.Join("&", Keys.Cast<string>().Select(key => $"{key}={Get(key)}"));
        }
    }
}