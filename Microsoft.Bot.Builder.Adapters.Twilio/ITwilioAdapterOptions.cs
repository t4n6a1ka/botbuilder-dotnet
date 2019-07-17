using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Bot.Builder.Adapters.Twilio
{
    public interface ITwilioAdapterOptions
    {
        string TwilioNumber { get; set; }

        string AccountSID { get; set; }

        string AuthToken { get; set; }

        string ValidationURL { get; set; }
    }
}
