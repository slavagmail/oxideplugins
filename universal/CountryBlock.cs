/*
TODO:
- Add command to check player's country, kick/ban optional
- Add optional customizable message when players connect
- Add notices about local IP, admin being excluded, etc.
- Add support for country, country code, and Steam ID in messages
- Switch to GeoIP API once plugin issues have been resolved
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("CountryBlock", "Wulf/lukespragg", "1.2.3", ResourceId = 1920)]
    [Description("Block or allow players only from configured countries")]

    class CountryBlock : CovalencePlugin
    {
        // Do NOT edit this file, instead edit CountryBlock.json in oxide/config and CountryBlock.en.json in the oxide/lang directory,
        // or create a new language file for another language using the 'en' file as a default

        #region Initialization

        const string permBypass = "countryblock.bypass";

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permBypass, this);
            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "This server doesn't allow players from {0}" }, this);
        }

        #endregion

        #region Configuration

        bool adminExcluded;
        bool banInstantly;
        List<object> countryList;
        bool whitelist;

        protected override void LoadDefaultConfig()
        {
            Config["AdminExcluded"] = adminExcluded = GetConfig("AdminExcluded", true);
            Config["BanInstantly"] = banInstantly = GetConfig("BanInstantly", false);
            Config["CountryList"] = countryList = GetConfig("CountryList", new List<object> { "CN", "RU" });
            Config["Whitelist"] = whitelist = GetConfig("Whitelist", false);
            SaveConfig();
        }

        #endregion

        #region Country Lookup

        void IsCountryBlocked(IPlayer player)
        {
            var ip = IpAddress(player.Address);

            #if DEBUG
            PrintWarning($"Local: {IsLocalIp(ip)}, Admin: {(adminExcluded && IsAdmin(player.Id))}, Perm: {HasPermission(player.Id, permBypass)}");
            #endif

            if (IsLocalIp(ip) || (adminExcluded && IsAdmin(player.Id)) || HasPermission(player.Id, permBypass)) return;

            var providers = new[]
            {
                $"http://ip-api.com/line/{ip}?fields=countryCode",
                $"http://legacy.iphub.info/api.php?showtype=4&ip={ip}",
                $"http://geoip.nekudo.com/api/{ip}",
                $"http://ipinfo.io/{ip}/country"
            };
            var url = providers[new Random().Next(providers.Length)];
            webrequest.EnqueueGet(url, (code, response) =>
            {
                #if DEBUG
                PrintWarning(url);
                PrintWarning(response);
                #endif

                if (code != 200 || string.IsNullOrEmpty(response) || response == "undefined" || response == "xx")
                {
                    PrintWarning($"Getting country for {player.Name} ({ip}) failed! ({code})");
                    IsCountryBlocked(player);
                    return;
                }

                var country = "unknown";
                try
                {
                    var json = JObject.Parse(response);
                    if (json["country"] != null) country = (string)json["country"]["code"];
                    else if (json["countryCode"] != null) country = (string)json["countryCode"];
                }
                catch
                {
                    country = Regex.Replace(response, @"\t|\n|\r|\s.*", "");
                }

                #if DEBUG
                PrintWarning($"Country response was {country} for {ip}");
                #endif

                if ((!countryList.Contains(country) && whitelist) || (countryList.Contains(country) && !whitelist))
                    PlayerRejection(player, country);
            }, this);
        }

        #endregion

        #region Player Rejection

        void PlayerRejection(IPlayer player, string country)
        {
            if (banInstantly)
                player.Ban(Lang("NotAllowed", player.Id, country), TimeSpan.Zero);
            else
                player.Kick(Lang("NotAllowed", player.Id, country));
        }

        void OnUserConnected(IPlayer player) => IsCountryBlocked(player);

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        bool IsAdmin(string id) => permission.UserHasGroup(id, "admin");

        static bool IsLocalIp(string ipAddress)
        {
            var split = ipAddress.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            var ip = new[] { int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]) };
            return ip[0] == 10 || ip[0] == 127 || (ip[0] == 192 && ip[1] == 168) || (ip[0] == 172 && (ip[1] >= 16 && ip[1] <= 31));
        }

        static string IpAddress(string ip)
        {
            #if DEBUG
            return "8.8.8.8"; // US
            #else
            return Regex.Replace(ip, @":{1}[0-9]{1}\d*", "");
            #endif
        }

        #endregion
    }
}
