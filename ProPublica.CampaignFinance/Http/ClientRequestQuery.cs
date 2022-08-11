using System;
using System.Collections.Generic;
using System.Linq;

namespace ProPublica.CampaignFinance.Http
{
    public sealed class ClientRequestQuery : Dictionary<string, object>
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
            return string.Join("&", Keys.Select((key, value) => $"{key}={value}"));
        }
    }
}