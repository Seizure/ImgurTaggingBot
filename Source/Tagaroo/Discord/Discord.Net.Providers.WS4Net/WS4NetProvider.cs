using Discord.Net.WebSockets;

/*
Copied directly from Discord.NET 1.0.2 source code, <//github.com./RogueException/Discord.Net/releases/tag/1.0.2>.
Discord.NET's WS4Net library targets the .NET Framework instead of the .NET Standard,
although this appears to have been since fixed (by commit on 2017-09-11).
Until this fix is available, the source code for the WS4Net library is incorporated directly into this project
so that it may be used on a non-.NET Framework platform.
*/

namespace Discord.Net.Providers.WS4Net
{
    public static class WS4NetProvider
    {
        public static readonly WebSocketProvider Instance = () => new WS4NetClient();
    }
}
