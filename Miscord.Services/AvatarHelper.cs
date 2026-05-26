using System;

namespace Miscord.Services
{
    public static class AvatarHelper
    {
        public static readonly string[] AVATAR_COLORS = {
            "#e91e63", "#9c27b0", "#673ab7", "#3f51b5",
            "#1976d2", "#0097a7", "#388e3c", "#f57c00",
            "#e64a19", "#c62828", "#00897b", "#43a047",
            "#fb8c00", "#6d4c41", "#8e24aa", "#1e88e5"
        };

        public static string GetUserColor(string username)
        {
            int hash = 0;
            string s = username ?? "?";
            for (int i = 0; i < s.Length; i++)
            {
                hash = (s[i] + ((hash << 5) - hash));
            }
            return AVATAR_COLORS[Math.Abs(hash) % AVATAR_COLORS.Length];
        }
    }
}