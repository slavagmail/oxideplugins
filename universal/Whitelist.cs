using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Whitelist", "Wulf/lukespragg", "3.1.0", ResourceId = 1932)]
    [Description("Restricts server access to whitelisted players only")]

    class Whitelist : CovalencePlugin
    {
        // Do NOT edit this file, instead edit Whitelist.json in oxide/config and Whitelist.en.json in the oxide/lang directory,
        // or create a new language file for another language using the 'en' file as a default

        #region Initialization

        const string permWhitelist = "whitelist.allowed";
        bool adminExcluded;
        bool resetOnRestart;

        protected override void LoadDefaultConfig()
        {
            Config["AdminExcluded"] = adminExcluded = GetConfig("AdminExcluded", true);
            Config["ResetOnRestart"] = resetOnRestart = GetConfig("ResetOnRestart", false);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permWhitelist, this);
            lang.RegisterMessages(new Dictionary<string, string> { ["NotWhitelisted"] = "You are not whitelisted" }, this);

            if (!resetOnRestart) return;
            foreach (var group in permission.GetGroups())
                if (permission.GroupHasPermission(group, permWhitelist)) permission.RevokeGroupPermission(group, permWhitelist);
            foreach (var user in permission.GetPermissionUsers(permWhitelist))
                permission.RevokeUserPermission(Regex.Replace(user, "[^0-9]", ""), permWhitelist);
        }

        #endregion

        #region Whitelist Check

        object CanUserLogin(string name, string id) => !IsWhitelisted(id) ? Lang("NotWhitelisted", id) : null;

        bool IsWhitelisted(string id)
        {
            var player = players.GetPlayer(id);
            return adminExcluded && player != null && player.IsAdmin || HasPermission(id, permWhitelist);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion
    }
}
