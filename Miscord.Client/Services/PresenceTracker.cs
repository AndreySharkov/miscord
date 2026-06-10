using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Miscord.Client.Services
{
    public interface IPresenceTracker
    {
        void UserConnected(string userId, string connectionId);
        void UserDisconnected(string userId, string connectionId);
        string[] GetOnlineUsers();
        bool IsUserOnline(string userId);
    }

    public class PresenceTracker : IPresenceTracker
    {
        // UserId -> Set of ConnectionIds
        private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();

        public void UserConnected(string userId, string connectionId)
        {
            OnlineUsers.AddOrUpdate(userId, 
                new HashSet<string> { connectionId }, 
                (key, oldValue) => {
                    lock(oldValue)
                    {
                        oldValue.Add(connectionId);
                    }
                    return oldValue;
                });
        }

        public void UserDisconnected(string userId, string connectionId)
        {
            if (OnlineUsers.TryGetValue(userId, out var connections))
            {
                lock(connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        OnlineUsers.TryRemove(userId, out _);
                    }
                }
            }
        }

        public string[] GetOnlineUsers()
        {
            return OnlineUsers.Keys.ToArray();
        }

        public bool IsUserOnline(string userId)
        {
            return OnlineUsers.ContainsKey(userId);
        }
    }
}
