using System.Net;

namespace DisControl.Bot;

public class BetterWebClient : WebClient
{
    protected override WebRequest GetWebRequest(Uri uri)
    {
        WebRequest w = base.GetWebRequest(uri);
        w.Timeout = 1000 * 60 * 10;
        return w;
    }
}